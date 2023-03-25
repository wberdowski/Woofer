using Discord.Audio;
using Microsoft.Extensions.Logging;
using System.Buffers;
using Un4seen.Bass;
using Un4seen.Bass.AddOn.WebM;
using Woofer.Core.Common;

namespace Woofer.Core.Audio
{
    internal class AudioPlayer : IDisposable
    {
        public ITrack? CurrentTrack => _currentTrack;
        public List<ITrack> TrackQueue => _trackQueue;
        static BASSError BassStatus => Bass.BASS_ErrorGetCode();

        readonly ILogger _logger;
        readonly AudioOutStream _outputStream;
        private readonly ArrayPool<byte> _pool;
        CancellationTokenSource? _playbackCts;
        Task? _playbackTask;
        int _playbackHandle;
        ManualResetEvent _isPaused = new(false);
        ITrack? _currentTrack;
        List<ITrack> _trackQueue = new();
        volatile bool _disableAutoplay = false;

        public AudioPlayer(IAudioClient audioClient, ILogger<AudioPlayerManager> logger)
        {
            _logger = logger;
            _outputStream = audioClient.CreatePCMStream(
                AudioApplication.Music
            //packetLoss: 100 // TODO
            );
            _pool = ArrayPool<byte>.Create();
        }

        public void Pause() => _isPaused.Reset();
        public void Unpause()
        {
            _isPaused.Set();
            _disableAutoplay = false;
        }

        void SetCurrentTrack(ITrack track) => _currentTrack = track;
        void ClearCurrentTrack() => _currentTrack = null;

        public void Enqueue(ITrack track)
        {
            TrackQueue.Add(track);

            if (_currentTrack == null && _trackQueue.Count == 1)
            {
                TryConsumeAndPlay();
            }
        }

        public async Task Stop()
        {
            _disableAutoplay = true;
            _logger.LogDebug("STOP."); LogState();
            _playbackCts?.Cancel();
            _isPaused.Set();

            await WaitForPlaybackTaskFinished();
        }

        public async Task Skip()
        {
            _disableAutoplay = true;
            _logger.LogDebug("SKIP."); LogState();
            _playbackCts?.Cancel();
            _isPaused.Set();

            await WaitForPlaybackTaskFinished();

            TryConsumeAndPlay();
        }

        async Task WaitForPlaybackTaskFinished()
        {
            if (_playbackTask != null && !_playbackTask.IsCanceled && !_playbackTask.IsCompleted)
            {
                _logger.LogDebug("Stopping task."); LogState();
                await _playbackTask;
                _logger.LogDebug("Task stopped."); LogState();
            }
        }

        void LogState()
        {
            _logger.LogDebug(
                $"\nSTATE:\n" +
                $"{nameof(_playbackTask)}={_playbackTask}\n" +
                $"{nameof(_playbackTask)} canceled={_playbackTask?.IsCanceled}\n" +
                $"{nameof(_playbackTask)} completed={_playbackTask?.IsCompleted}\n" +
                $"{nameof(_disableAutoplay)}={_disableAutoplay}\n"
            );
        }

        bool TryConsumeAndPlay()
        {
            if (!TrackQueue.Any())
            {
                return false;
            }

            _disableAutoplay = false;
            _playbackTask?.Wait();

            var track = TrackQueue.First();
            TrackQueue.RemoveAt(0);

            _playbackCts = new CancellationTokenSource();

            _logger.LogDebug($"Now playing: {track}"); LogState();
            _playbackTask = Task.Run(() => StreamTrack(track), _playbackCts.Token);
            _playbackTask.ContinueWith(t =>
            {
                _logger.LogDebug("Task finished."); LogState();

                if (!_disableAutoplay)
                {
                    _logger.LogDebug("Autoplay next."); LogState();
                    TryConsumeAndPlay();
                }
            });
            _playbackTask.Exception?.Handle(HandleException);

            return true;
        }

        bool HandleException(Exception ex)
        {
            _logger.LogError("[Play] {Message}", ex); LogState();
            ClearCurrentTrack();

            return true;
        }

        async Task StreamTrack(ITrack track)
        {
            SetCurrentTrack(track);
            _isPaused.Set();

            _playbackHandle = BassWebM.BASS_WEBM_StreamCreateURL(
                track.AudioSource.Url,
                0,
                BASSFlag.BASS_STREAM_DECODE | BASSFlag.BASS_STREAM_PRESCAN,
                null,
                IntPtr.Zero,
                0
            );

            var bufferSize = (int)Bass.BASS_ChannelSeconds2Bytes(_playbackHandle, 0.1f);
            var sampleBuffer = _pool.Rent(bufferSize);

            try
            {
                while (Bass.BASS_ChannelIsActive(_playbackHandle) == BASSActive.BASS_ACTIVE_PLAYING)
                {
                    _isPaused.WaitOne();
                    _playbackCts?.Token.ThrowIfCancellationRequested();
                    var bytesRead = Bass.BASS_ChannelGetData(_playbackHandle, sampleBuffer, bufferSize);

                    if (bytesRead > 0)
                    {
                        _outputStream.Write(sampleBuffer, 0, bytesRead);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug($"Playback cancelled: {track.Title}"); LogState();
            }
            finally
            {

                await _outputStream.FlushAsync();
                BassWebM.FreeMe();
                _pool.Return(sampleBuffer);
            }

            _logger.LogDebug($"Playback ended: {track.Title}"); LogState();

            ClearCurrentTrack();
        }

        public void Dispose()
        {
            BassWebM.FreeMe();
            _outputStream.Dispose();
            _outputStream.Flush();
        }
    }
}
