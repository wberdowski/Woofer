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
        private IAudioClient? _audioClient;
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
                lock (_streamLock)
                {
                    _audioClient?.StopAsync().Wait();
                    _audioClient?.Dispose();

                    _audioClient = audioClient;

                    _outputStream = _audioClient.CreatePCMStream(
                        AudioApplication.Music
                    );
                }
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
                    TryStopConsumeAndPlay().Wait();
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
                track = _currentTrack;

                InternalStop().Wait();
            }

            return Task.FromResult(track);
        }

        public Task<Track?> Skip()
        {
            Track? track = null;

            lock (_controlLock)
            {
                track = _currentTrack;

                InternalSkip().Wait();
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
            await TryStopConsumeAndPlay();
        }

        private void SetCurrentTrack(Track track) => _currentTrack = track;
        private void ClearCurrentTrack() => _currentTrack = null;

        private async Task WaitForPlaybackTaskFinished()
        {
            if (_playbackTask != null /*&& (_playbackCts != null && !_playbackCts.IsCancellationRequested)*/)
            {
                _logger.LogDebug("Stopping task.");
                await _playbackTask;
                _logger.LogDebug("Task stopped.");
            }
        }

        private async Task TryStopConsumeAndPlay(bool isInvokedByAutoplay = false)
        {
            if (!TrackQueue.Any())
            {
                await Task.FromResult(false);
            }

            if (!isInvokedByAutoplay)
            {
                await InternalStop();
            }

            var track = TrackQueue.First();
            TrackQueue.RemoveAt(0);

            _playbackCts = new CancellationTokenSource();

            _logger.LogDebug($"Now playing (Autoplay? {isInvokedByAutoplay}): {track}");

            _playbackTask = Task.Run(() =>
                StreamTrack(track, _playbackCts.Token),
                _playbackCts.Token
            ).ContinueWith((t) =>
            {
                if (t.IsFaulted)
                {
                    HandleException(t.Exception);
                }
            });

            await Task.FromResult(true);
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

            var result = TryStopConsumeAndPlay(true);
            _logger.LogDebug($"Autoplay next = {result}");
        }

        public void Dispose()
        {
            Bass.Free();
            _outputStream?.Dispose();
            _outputStream?.Flush();
        }
    }
}
