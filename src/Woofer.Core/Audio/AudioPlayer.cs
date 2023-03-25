﻿using Discord.Audio;
using ManagedBass;
using Microsoft.Extensions.Logging;
using System.Buffers;
using Woofer.Core.Common;

namespace Woofer.Core.Audio
{
    internal class AudioPlayer : IDisposable
    {
        public ITrack? CurrentTrack => _currentTrack;
        public List<ITrack> TrackQueue => _trackQueue;

        private readonly ILogger _logger;
        private readonly AudioOutStream _outputStream;
        private readonly ArrayPool<byte> _pool;
        private CancellationTokenSource? _playbackCts;
        private Task? _playbackTask;
        private int _playbackHandle;
        private readonly ManualResetEvent _isPaused = new(false);
        private ITrack? _currentTrack;
        private readonly List<ITrack> _trackQueue = new();
        private volatile bool _disableAutoplay = false;
        private volatile object _controlLock = new();

        public AudioPlayer(IAudioClient audioClient, ILogger<AudioPlayerManager> logger)
        {
            _logger = logger;
            _outputStream = audioClient.CreatePCMStream(
                AudioApplication.Music
            //packetLoss: 100 // TODO
            );
            _pool = ArrayPool<byte>.Create();
        }

        public async Task Pause()
        {
            lock (_controlLock)
            {
                _isPaused.Reset();
            }

            await Task.CompletedTask;
        }

        public async Task Unpause()
        {
            lock (_controlLock)
            {
                _isPaused.Set();
                _disableAutoplay = false;
            }

            await Task.CompletedTask;
        }

        public async Task Enqueue(ITrack track)
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

        public Task<ITrack?> Stop()
        {
            ITrack? track = null;

            lock (_controlLock)
            {
                _logger.LogDebug("STOP.");

                track = CurrentTrack;

                _disableAutoplay = true;
                _playbackCts?.Cancel();
                _isPaused.Set();

                WaitForPlaybackTaskFinished().Wait();
            }

            return Task.FromResult(track);
        }

        public Task<ITrack?> Skip()
        {
            ITrack? track = null;

            lock (_controlLock)
            {
                _logger.LogDebug("SKIP.");

                track = CurrentTrack;

                _disableAutoplay = true;
                _playbackCts?.Cancel();
                _isPaused.Set();

                WaitForPlaybackTaskFinished().Wait();

                TryConsumeAndPlay();
            }

            return Task.FromResult(track);
        }

        private void SetCurrentTrack(ITrack track) => _currentTrack = track;
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

            _disableAutoplay = false;
            _playbackTask?.Wait();

            var track = TrackQueue.First();
            TrackQueue.RemoveAt(0);

            _playbackCts = new CancellationTokenSource();

            _logger.LogDebug($"Now playing (Autoplay? {isInvokedByAutoplay}): {track}");
            _playbackTask = Task.Run(() => StreamTrack(track), _playbackCts.Token);
            _playbackTask.ContinueWith(t =>
            {
                _logger.LogDebug("Task finished.");

                if (!_disableAutoplay)
                {
                    _logger.LogDebug("Autoplay next.");
                    TryConsumeAndPlay(true);
                }
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

        private async Task StreamTrack(ITrack track)
        {
            SetCurrentTrack(track);
            _isPaused.Set();

            _playbackHandle = Bass.CreateStream(
                track.AudioSource.Url,
                0,
                BassFlags.Decode | BassFlags.Prescan,
                null
            );

            var bufferSize = (int)Bass.ChannelSeconds2Bytes(_playbackHandle, 0.1f);
            var sampleBuffer = _pool.Rent(bufferSize);

            await _outputStream.FlushAsync();
            _outputStream.Clear();

            try
            {
                while (Bass.ChannelIsActive(_playbackHandle) == PlaybackState.Playing)
                {
                    _isPaused.WaitOne();
                    _playbackCts?.Token.ThrowIfCancellationRequested();

                    var bytesRead = Bass.ChannelGetData(_playbackHandle, sampleBuffer, bufferSize);

                    if (bytesRead > 0)
                    {
                        _outputStream.Write(sampleBuffer, 0, bytesRead);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug($"Playback cancelled: {track.Title}");
            }
            finally
            {

                await _outputStream.FlushAsync();
                Bass.StreamFree(_playbackHandle);
                _pool.Return(sampleBuffer);
            }

            _logger.LogDebug($"Playback ended: {track.Title}");

            ClearCurrentTrack();
        }

        public void Dispose()
        {
            Bass.Free();
            _outputStream.Dispose();
            _outputStream.Flush();
        }
    }
}
