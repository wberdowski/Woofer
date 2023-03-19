using Discord;
using Discord.Audio;
using Microsoft.Extensions.Logging;
using Un4seen.Bass;
using Un4seen.Bass.AddOn.WebM;
using Woofer.Core.Common;

namespace Woofer.Core.Audio
{
    internal class AudioPlayer : IDisposable
    {
        public IAudioChannel AudioChannel { get; }
        public IAudioClient AudioClient { get; }
        public ITrack? CurrentTrack { get; private set; }
        public List<ITrack> TrackQueue { get; private set; }

        private AudioOutStream _outputStream;
        private CancellationTokenSource _cts;
        private Task? _playbackTask;
        private readonly ILogger _logger;
        private byte[] _sampleBuffer = null; // our local buffer
        private int _handle;
        private int _bytesread;
        private bool _isPaused;

        private Guid id = Guid.NewGuid();

        public AudioPlayer(IVoiceChannel audioChannel, IAudioClient audioClient, ILogger<AudioPlayerManager> logger)
        {
            AudioChannel = audioChannel;
            AudioClient = audioClient;
            _logger = logger;
            TrackQueue = new List<ITrack>();

            _outputStream = audioClient.CreatePCMStream(
                AudioApplication.Music,
                packetLoss: 100 // TODO
            );

            Bass.BASS_Init(-1, -1, BASSInit.BASS_DEVICE_NOSPEAKER, IntPtr.Zero);
            Bass.BASS_PluginLoad("bassopus.dll");

            var bassStatus = Bass.BASS_ErrorGetCode();

            if (bassStatus != BASSError.BASS_OK)
            {
                throw new Exception("BASS: " + bassStatus);
            }
        }

        public void Enqueue(ITrack track, IUserMessage? reply = null)
        {
            TrackQueue.Add(track);

            if (CurrentTrack == null && TrackQueue.Count == 1)
            {
                ConsumeAndPlay();
            }
        }

        public void Pause()
        {
            if (_playbackTask != null)
            {
                _isPaused = true;
            }
        }

        public void Resume()
        {
            if (_playbackTask != null)
            {
                _isPaused = false;
            }
        }

        public async Task Stop()
        {
            _cts?.Cancel();
            if (_playbackTask != null)
            {
                await _playbackTask;
            }
        }

        public async Task Skip()
        {
            await Stop();

            if (TrackQueue.Any())
            {
                ConsumeAndPlay();
            }
        }

        private void ConsumeAndPlay()
        {
            if (!TrackQueue.Any())
            {
                throw new InvalidOperationException("Queue is empty.");
            }

            var track = TrackQueue.First();
            TrackQueue.RemoveAt(0);

            _cts = new CancellationTokenSource();
            _playbackTask = Task.Run(() => StreamTrack(track, _cts.Token));
            _playbackTask.Exception?.Handle(HandleException);
        }

        private bool HandleException(Exception ex)
        {
            _logger.LogError("[Play] {Message}", ex);
            return true;
        }

        private async Task StreamTrack(ITrack track, CancellationToken token)
        {
            CurrentTrack = track;
            _isPaused = false;

            try
            {
                _handle = BassWebM.BASS_WEBM_StreamCreateURL(track.AudioSource.Url, 0, BASSFlag.BASS_STREAM_DECODE | BASSFlag.BASS_STREAM_PRESCAN, null, IntPtr.Zero, 0);
                _sampleBuffer = new byte[(int)Bass.BASS_ChannelSeconds2Bytes(_handle, 0.1f)];

                while (Bass.BASS_ChannelIsActive(_handle) == BASSActive.BASS_ACTIVE_PLAYING)
                {
                    while (_isPaused)
                    {
                        await Task.Delay(1);
                    };

                    token.ThrowIfCancellationRequested();

                    _bytesread = Bass.BASS_ChannelGetData(_handle, _sampleBuffer, _sampleBuffer.Length);

                    if (_bytesread != 0)
                    {
                        _outputStream.Write(_sampleBuffer, 0, _bytesread);
                    }
                }

                var bassStatus = Bass.BASS_ErrorGetCode();

                if (bassStatus != BASSError.BASS_OK)
                {
                    _logger.LogError(bassStatus.ToString());
                    return;
                }
            }
            finally
            {
                await _outputStream.FlushAsync();
                BassWebM.FreeMe();
            }

            _logger.LogDebug($"Song finished: {track.Title}");

            CurrentTrack = null;
        }

        public void Dispose()
        {
            // TODO
        }
    }
}
