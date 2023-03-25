using Discord;
using Discord.Audio;
using Discord.WebSocket;
using ManagedBass;
using Microsoft.Extensions.Logging;

namespace Woofer.Core.Audio
{
    internal class AudioPlayerManager
    {
        public Dictionary<ulong, AudioPlayer> AudioPlayers { get; set; }
        private readonly DiscordSocketClient _client;
        private readonly ILogger<AudioPlayerManager> _logger;

        public AudioPlayerManager(DiscordSocketClient client, ILogger<AudioPlayerManager> logger)
        {
            AudioPlayers = new Dictionary<ulong, AudioPlayer?>();
            _client = client;
            _logger = logger;

            Bass.Init(-1, -1, DeviceInitFlags.NoSpeakerAssignment, IntPtr.Zero);
            Bass.PluginLoad("bassopus.dll");
            Bass.PluginLoad("basswebm.dll");

            if (Bass.LastError != Errors.OK)
            {
                throw new Exceptions.BassException(Bass.LastError);
            }
        }

        public async Task<AudioPlayer?> RequestAudioPlayerAtChannel(ulong guildId, IVoiceChannel channel)
        {
            IAudioClient? audioClient;
            AudioPlayer audioPlayer;

            if (await IsAudioPlayerConnected(channel))
            {
                if (AudioPlayers.TryGetValue(guildId, out audioPlayer) /*&& audioPlayer.AudioClient.ConnectionState == ConnectionState.Connected*/)
                {
                    _logger.LogDebug($"Reusing {nameof(AudioPlayer)}.");
                    return audioPlayer;
                }

                _logger.LogDebug($"Player connected, but no {nameof(AudioPlayer)} found.");
                audioClient = await channel.ConnectAsync(true);

                audioPlayer = new AudioPlayer(audioClient, _logger);
                AudioPlayers.Add(guildId, audioPlayer);

                return audioPlayer;
            }

            AudioPlayers.Remove(guildId);

            _logger.LogDebug($"Creating new {nameof(AudioPlayer)}.");
            audioClient = await channel.ConnectAsync(true);
            audioPlayer = new AudioPlayer(audioClient, _logger);

            AudioPlayers.Add(guildId, audioPlayer);

            return audioPlayer;
        }

        public AudioPlayer? GetAudioPlayer(ulong guildId)
        {
            if (AudioPlayers.TryGetValue(guildId, out var player))
            {
                return player;
            }

            return null;
        }

        public async Task DisposeAudioPlayer(ulong guildId)
        {
            if (AudioPlayers.TryGetValue(guildId, out var player))
            {
                player?.Dispose();
                AudioPlayers.Remove(guildId);
            }
        }

        private async Task<bool> IsAudioPlayerConnected(IVoiceChannel channel)
        {
            return await channel.GetUserAsync(_client.CurrentUser.Id) != null;
        }
    }
}
