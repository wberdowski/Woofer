using Discord;
using Discord.Audio;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using Woofer.Core.Common;

namespace Woofer.Core.Audio
{
    public class AudioPlayer : IDisposable
    {
        public IAudioChannel AudioChannel { get; }
        public IAudioClient AudioClient { get; }
        public List<ITrack> TrackQueue { get; private set; }
        public Action<AudioPlayer, TrackMessageEventArgs> TrackEnqueued;
        public Action<AudioPlayer> PlaybackPaused;
        public Action<AudioPlayer, TrackMessageEventArgs> TrackUpdated;

        private AudioOutStream _outputStream;
        private CancellationTokenSource _cts;
        private Guid _guid;
        private Task? _playbackTask;
        private const float Volume = 0.5f;

        public AudioPlayer(IVoiceChannel audioChannel, IAudioClient audioClient)
        {
            AudioChannel = audioChannel;
            AudioClient = audioClient;
            TrackQueue = new List<ITrack>();

            _outputStream = audioClient.CreatePCMStream(
                AudioApplication.Music,
                null,
                1000,
                0
            );

            _guid = Guid.NewGuid();
        }

        public async Task Enqueue(ITrack track, IUserMessage? reply = null)
        {
            TrackQueue.Add(track);

            if (TrackQueue.Count == 1)
            {
                await PlayCurrentTrack(true, reply);
            }
            else
            {
                TrackEnqueued?.Invoke(this, new TrackMessageEventArgs(track, reply));
            }
        }

        // TODO: Pause current song, play new song, restore previously playing song
        public async Task PlayNow(ITrack track)
        {
            if (TrackQueue.First() != track)
            {
                TrackQueue.Remove(track);

                await Stop();
                await RemoveCurrentTrack();
                TrackQueue.Insert(0, track);
                await PlayCurrentTrack();
            }
        }

        public async Task ClearQueue()
        {
            await Stop();

            TrackQueue.Clear();
        }

        public async Task RemoveTrack(int index)
        {
            index--;

            if (TrackQueue.Any() && index >= 0)
            {
                if (index == 0)
                {
                    await Skip();
                    return;
                }
                else if (TrackQueue.Count > index)
                {
                    TrackQueue.RemoveAt(index);
                    return;
                }
            }

            // Specified index is out of range
        }

        public async Task MoveTrack(int index, int destPos)
        {
            index--;
            destPos--;

            if (index > 0 && destPos > 0 && index != destPos && destPos < TrackQueue.Count)
            {
                var track = TrackQueue[index];
                TrackQueue.RemoveAt(index);
                TrackQueue.Insert(destPos, track);
            }

            await Task.CompletedTask;
        }

        public async Task PlayNext(ITrack track)
        {
            if (TrackQueue.First() != track)
            {
                TrackQueue.Remove(track);
                TrackQueue.Insert(1, track);
            }

            await Task.CompletedTask;
        }

        public async Task RemoveTrack(ITrack track)
        {
            if (TrackQueue.First() == track)
            {
                await Skip();
            }
            else
            {
                TrackQueue.Remove(track);
            }
        }

        public async Task Skip(bool forcedByUser = false)
        {
            await Stop();
            await RemoveCurrentTrack();
            await PlayCurrentTrack(forcedByUser);
        }

        private async Task RemoveCurrentTrack()
        {
            lock (TrackQueue)
            {
                TrackQueue.RemoveAt(0);
            }

            await Task.CompletedTask;
        }

        private async Task PlayCurrentTrack(bool forcedByUser = true, IUserMessage? reply = null)
        {
            if (TrackQueue.Count > 0)
            {
                var track = TrackQueue.First();

                _cts = new CancellationTokenSource();
                _playbackTask = PlayAsync(track, _cts.Token);
                _playbackTask?.Exception?.Handle(HandleException);

                if (forcedByUser)
                {
                    TrackUpdated?.Invoke(this, new TrackMessageEventArgs(track, reply));
                }
            }

            await Task.CompletedTask;
        }

        private bool HandleException(Exception ex)
        {
            //Logger.Error("[Play] {Message}", ex);

            return true;
        }

        public async Task Pause()
        {
            //Logger.Information("Track paused - " + _guid);

            await Stop();

            PlaybackPaused?.Invoke(this);
        }

        public async Task Stop()
        {
            _cts?.Cancel();
            if (_playbackTask != null)
            {
                await _playbackTask;
            }

            _outputStream?.Clear();
            _outputStream?.Flush();
        }

        private async Task PlayAsync(ITrack track, CancellationToken token)
        {
            //Logger.Information("Play - " + track.Title);

            using (var ffmpeg = CreateStream(track.AudioSource.Url))
            {
                if (ffmpeg != null)
                {
                    using (var inputStream = ffmpeg.StandardOutput.BaseStream)
                    {
                        try
                        {
                            await inputStream.CopyToAsync(_outputStream, token);

                        }
                        catch (OperationCanceledException)
                        {
                            //Logger.Information("Playback cancelled: " + track.Title);
                        }
                        finally
                        {
                            await _outputStream.FlushAsync();
                            ffmpeg.Kill();
                            //Logger.Information("Track finished: " + track.Title);
                            _playbackTask = null;
                            await Skip(false);
                        }
                    }
                }
                else
                {
                    //Logger.Error("FFmpeg process could not be created");
                }
            }
        }

        private Process? CreateStream(string path)
        {
            var ffmpegPath = "ffmpeg";

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                ffmpegPath = "/usr/bin/ffmpeg";
            }

            var proc = Process.Start(new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = $"-hide_banner -loglevel error -reconnect 1 -reconnect_at_eof 1 -reconnect_streamed 1 -reconnect_delay_max 2 -i \"{path}\" -filter:a \"volume = {Volume.ToString("0.#", CultureInfo.InvariantCulture)}\" -ac 2 -f wav -ar 48000 pipe:",
                UseShellExecute = false,
                RedirectStandardOutput = true
            });

            if (proc != null && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                proc.PriorityClass = ProcessPriorityClass.RealTime;
            }

            return proc;
        }

        public async void Dispose()
        {
            await Stop();
            TrackQueue = null;
            try
            {
                _outputStream?.Close();
                _outputStream?.Dispose();
            }
            catch (ObjectDisposedException)
            {

            }
        }

        ~AudioPlayer()
        {
            Dispose();
        }
    }
}
