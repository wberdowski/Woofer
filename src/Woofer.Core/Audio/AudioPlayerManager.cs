using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace Woofer.Core.Audio
{
    internal class AudioPlayerManager
    {
        public Dictionary<ulong, AudioPlayer?> AudioPlayers { get; set; }
        private readonly DiscordSocketClient _client;
        private readonly ILogger<AudioPlayerManager> _logger;

        public AudioPlayerManager(DiscordSocketClient client, ILogger<AudioPlayerManager> logger)
        {
            AudioPlayers = new Dictionary<ulong, AudioPlayer?>();
            _client = client;
            _logger = logger;
        }

        public async Task<AudioPlayer> RequestAudioPlayerAtChannel(ulong guildId, IVoiceChannel channel)
        {
            if (await IsAudioPlayerConnected(channel))
            {
                if (AudioPlayers.TryGetValue(guildId, out var player))
                {
                    _logger.LogDebug("Reusing AudioPlayerInstace.");
                    return player;
                }
                else
                {
                    _logger.LogDebug("Player connected, but no AudioPlayerInstace found.");
                    var audioClient = await channel.ConnectAsync(true);
                    var audioPlayer = new AudioPlayer(channel, audioClient, _logger);

                    AudioPlayers.Add(guildId, audioPlayer);

                    return audioPlayer;
                }
            }
            else
            {
                AudioPlayers.Remove(guildId);

                _logger.LogDebug("Creating new AudioPlayerInstace.");
                var audioClient = await channel.ConnectAsync(true);
                var audioPlayer = new AudioPlayer(channel, audioClient, _logger);

                AudioPlayers.Add(guildId, audioPlayer);

                return audioPlayer;
            }
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
