using Discord.Audio;
using ManagedBass;
using Microsoft.Extensions.Logging;
using System.Buffers;

namespace Woofer.Core.Audio
{
    internal class AudioPlayer : IDisposable
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
        private readonly List<Track> _trackQueue = new();
        private readonly object _streamLock = new();
        private readonly object _controlLock = new();

        public AudioPlayer(ILogger<AudioPlayerManager> logger)
        {
            _logger = logger;
            _pool = ArrayPool<byte>.Create();
        }

        public Task SetAudioClient(IAudioClient audioClient)
        {
            lock (_controlLock)
            {
                InternalStop().Wait();

                _outputStream = audioClient.CreatePCMStream(
                    AudioApplication.Music
                //packetLoss: 100 // TODO
                );
            }

            return Task.CompletedTask;
        }

        public Task<Track?> Pause()
        {
            Track? track = null;

            lock (_controlLock)
            {
                track = _currentTrack;
                _isPaused.Reset();
            }

            return Task.FromResult(track);
        }

        public Task<Track?> Unpause()
        {
            Track? track = null;

            lock (_controlLock)
            {
                track = _currentTrack;
                _isPaused.Set();
            }

            return Task.FromResult(track);
        }

        public async Task Enqueue(Track track)
        {
            lock (_controlLock)
            {
                TrackQueue.Add(track);

                if (_currentTrack == null && _trackQueue.Count == 1)
                {
                    TryConsumeAndPlay();
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
                    InternalSkip().Wait();

                    return Task.FromResult(true);
                }
            }

            return Task.FromResult(false);
        }

        public Task<bool> TryEnqueueNext(Track track)
        {
            lock (_controlLock)
            {
                if (_currentTrack != track)
                {
                    TrackQueue.Remove(track);
                    TrackQueue.Insert(0, track);

                    return Task.FromResult(true);
                }
            }

            return Task.FromResult(false);
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
                    InternalSkip().Wait();
                }
            }

            await Task.CompletedTask;
        }

        public Task<Track?> Stop()
        {
            Track? track = null;

            lock (_controlLock)
            {
                _logger.LogDebug("STOP.");

                track = _currentTrack;

                InternalStop().Wait();
            }

            return Task.FromResult(track);
        }

        private async Task InternalStop()
        {
            _playbackCts?.Cancel();
            _isPaused.Set();

            await WaitForPlaybackTaskFinished();
        }

        private async Task InternalSkip()
        {
            await InternalStop();
            TryConsumeAndPlay();
        }

        public Task<Track?> Skip()
        {
            Track? track = null;

            lock (_controlLock)
            {
                _logger.LogDebug("SKIP.");

                track = _currentTrack;

                InternalSkip().Wait();
            }

            return Task.FromResult(track);
        }

        private void SetCurrentTrack(Track track) => _currentTrack = track;
        private void ClearCurrentTrack() => _currentTrack = null;

        private async Task WaitForPlaybackTaskFinished()
        {
            if (_playbackTask != null && !_playbackTask.IsCanceled && !_playbackTask.IsCompleted)
            {
                _logger.LogDebug("Stopping task.");
                await _playbackTask;
                _logger.LogDebug("Task stopped.");
            }
        }

        private bool TryConsumeAndPlay(bool isInvokedByAutoplay = false)
        {
            if (!TrackQueue.Any())
            {
                return false;
            }

            if (!isInvokedByAutoplay)
            {
                _playbackTask?.Wait();
            }

            var track = TrackQueue.First();
            TrackQueue.RemoveAt(0);

            _playbackCts = new CancellationTokenSource();

            _logger.LogDebug($"Now playing (Autoplay? {isInvokedByAutoplay}): {track}");
            _playbackTask = Task.Run(() => StreamTrack(track), _playbackCts.Token);
            _playbackTask.ContinueWith(t =>
            {
                _logger.LogDebug("Task finished.");
            });
            _playbackTask.Exception?.Handle(HandleException);

            return true;
        }

        private bool HandleException(Exception ex)
        {
            _logger.LogError("[Play] {Message}", ex);
            ClearCurrentTrack();

            return true;
        }

        private Task StreamTrack(Track track)
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
                var ex = new Exceptions.BassException(Bass.LastError);
                _logger.LogError(ex, ex.Message);
            }

            var bufferSize = (int)Bass.ChannelSeconds2Bytes(_playbackHandle, 0.1f);
            var sampleBuffer = _pool.Rent(bufferSize);

            try
            {
                while (Bass.ChannelIsActive(_playbackHandle) == PlaybackState.Playing)
                {
                    _isPaused.WaitOne();
                    _playbackCts?.Token.ThrowIfCancellationRequested();

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
                return Task.CompletedTask;
            }
            finally
            {
                _outputStream?.Flush();
                Bass.StreamFree(_playbackHandle);
                _pool.Return(sampleBuffer);
                ClearCurrentTrack();
                _logger.LogDebug($"Playback ended: {track.Title}");
            }

            var result = TryConsumeAndPlay(true);
            _logger.LogDebug($"Autoplay next = {result}");

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            Bass.Free();
            _outputStream?.Dispose();
            _outputStream?.Flush();
        }
    }
}
