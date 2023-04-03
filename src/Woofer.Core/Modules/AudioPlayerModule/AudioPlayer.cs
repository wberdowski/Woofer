using Discord.Audio;
using ManagedBass;
using Microsoft.Extensions.Logging;
using System.Buffers;

namespace Woofer.Core.Modules.AudioPlayerModule
{
    public class AudioPlayer
    {
        public Track? CurrentTrack => _currentTrack;
        public List<Track> TrackQueue => _trackQueue;

        private readonly ILogger _logger;
        private AudioOutStream? _outputStream;
        private readonly ArrayPool<byte> _pool;
        private CancellationTokenSource? _playbackCts;
        private Task? _playbackTask;
        private int _playbackHandle;
        private readonly ManualResetEvent _isPaused = new(false);
        private Track? _currentTrack;
        private IAudioClient? _audioClient;
        private readonly List<Track> _trackQueue = new();
        private readonly object _streamLock = new();
        private readonly object _controlLock = new();
        private const int WaitTimeout = 3000;

        public AudioPlayer(ILogger<AudioPlayerManager> logger)
        {
            _logger = logger;
            _pool = ArrayPool<byte>.Create();
        }

        public async Task SetAudioClient(IAudioClient audioClient)
        {
            lock (_controlLock)
            {
                lock (_streamLock)
                {
                    _audioClient?.StopAsync().Wait(WaitTimeout);
                    _audioClient?.Dispose();

                    _audioClient = audioClient;

                    _outputStream = _audioClient.CreatePCMStream(
                        AudioApplication.Music
                    );
                }
            }

            await Task.CompletedTask;
        }

        public Task<Track?> Pause()
        {
            lock (_controlLock)
            {
                var track = _currentTrack;
                _isPaused.Reset();
                return Task.FromResult(track);
            }
        }

        public Task<Track?> Unpause()
        {
            lock (_controlLock)
            {
                var track = _currentTrack;
                _isPaused.Set();
                return Task.FromResult(track);
            }
        }

        public async Task Enqueue(Track track)
        {
            lock (_controlLock)
            {
                TrackQueue.Add(track);

                if (_currentTrack == null && _trackQueue.Count == 1)
                {
                    TrySkipAndPlay().Wait(WaitTimeout);
                }
            }

            await Task.CompletedTask;
        }

        public Task<bool> TryPlayNow(Track track)
        {
            lock (_controlLock)
            {
                if (_currentTrack != track)
                {
                    TrackQueue.Remove(track);
                    TrackQueue.Insert(0, track);
                    TrySkipAndPlay().Wait(WaitTimeout);

                    return Task.FromResult(true);
                }

                return Task.FromResult(false);
            }
        }

        public Task<bool> TryEnqueueNext(Track track)
        {
            lock (_controlLock)
            {
                if (_currentTrack != track && _trackQueue.Any() && _trackQueue.First() != track)
                {
                    TrackQueue.Remove(track);
                    TrackQueue.Insert(0, track);

                    return Task.FromResult(true);
                }

                return Task.FromResult(false);
            }
        }

        public async Task Delete(Track track)
        {
            lock (_controlLock)
            {
                if (_currentTrack != track)
                {
                    TrackQueue.Remove(track);
                }
                else
                {
                    TrySkipAndPlay().Wait(WaitTimeout);
                }
            }

            await Task.CompletedTask;
        }

        public Task<Track?> Stop()
        {
            lock (_controlLock)
            {
                var track = _currentTrack;
                InternalStop().Wait(WaitTimeout);
                return Task.FromResult(track);
            }
        }

        public Task<Track?> Skip()
        {
            lock (_controlLock)
            {
                var track = _currentTrack;
                TrySkipAndPlay().Wait(WaitTimeout);
                return Task.FromResult(track);
            }
        }

        private async Task InternalStop()
        {
            _playbackCts?.Cancel();
            _isPaused.Set();

            await WaitForPlaybackTaskFinished();
        }

        private void SetCurrentTrack(Track track) => _currentTrack = track;
        private void ClearCurrentTrack() => _currentTrack = null;

        private async Task WaitForPlaybackTaskFinished()
        {
            if (_playbackTask != null)
            {
                _logger.LogDebug("Waiting for playback task to finish.");
                await _playbackTask;
                _logger.LogDebug("Playback task finished.");
            }
        }

        private async Task<bool> TrySkipAndPlay()
        {
            await InternalStop();

            if (!TrackQueue.Any())
            {
                return false;
            }

            var track = TrackQueue.First();
            TrackQueue.RemoveAt(0);

            _playbackCts = new CancellationTokenSource();

            _logger.LogDebug($"Now playing: {track}");

            _playbackTask = Task.Factory.StartNew(() =>
                StreamTrack(track, _playbackCts.Token),
                _playbackCts.Token,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default
            ).ContinueWith(async (t) =>
            {
                _playbackTask = null;

                if (t.IsFaulted)
                {
                    HandleException(t.Exception!);
                }
                else
                {
                    // Autoplay
                    await TrySkipAndPlay();
                }
            });

            return true;
        }

        private bool HandleException(Exception ex)
        {
            _logger.LogError("[Play] {Message}", ex);
            ClearCurrentTrack();

            return true;
        }

        private void StreamTrack(Track track, CancellationToken cancellationToken)
        {
            SetCurrentTrack(track);
            _isPaused.Set();

            _playbackHandle = Bass.CreateStream(
                track.AudioSource.Url,
                0,
                BassFlags.Decode,
                null
            );

            if (Bass.LastError != Errors.OK)
            {
                throw new Exceptions.BassException(Bass.LastError);
            }

            var bufferSize = (int)Bass.ChannelSeconds2Bytes(_playbackHandle, 0.1f);
            var sampleBuffer = _pool.Rent(bufferSize);

            try
            {
                while (Bass.ChannelIsActive(_playbackHandle) == PlaybackState.Playing)
                {
                    _isPaused.WaitOne();
                    cancellationToken.ThrowIfCancellationRequested();

                    var bytesRead = Bass.ChannelGetData(_playbackHandle, sampleBuffer, bufferSize);

                    if (bytesRead > 0)
                    {
                        lock (_streamLock)
                        {
                            _outputStream?.Write(sampleBuffer, 0, bytesRead);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug($"Playback cancelled: {track.Title}");
                throw;
            }
            finally
            {
                _outputStream?.Flush();
                Bass.StreamFree(_playbackHandle);
                _pool.Return(sampleBuffer);
                ClearCurrentTrack();
                _logger.LogDebug($"Playback ended: {track.Title}");
            }
        }

        public async Task DisconnectAndDispose()
        {
            await InternalStop();

            if (_audioClient != null)
            {
                await _audioClient.StopAsync();
                _audioClient.Dispose();
            }

            if (_outputStream != null)
            {
                _outputStream.Flush();
                _outputStream.Dispose();
            }
        }
    }
}
