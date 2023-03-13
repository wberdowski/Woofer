using Discord;
using Discord.WebSocket;

namespace Woofer.Core.Audio
{
    public class AudioPlayerManager
    {
        public Dictionary<ulong, AudioPlayer?> AudioPlayers { get; set; }
        private readonly DiscordSocketClient _client;

        public AudioPlayerManager(DiscordSocketClient client)
        {
            AudioPlayers = new Dictionary<ulong, AudioPlayer?>();
            _client = client;
        }

        public async Task<AudioPlayer> RequestAudioPlayerAtChannel(ulong guildId, IVoiceChannel channel)
        {
            if (await IsAudioPlayerConnected(channel))
            {
                if (AudioPlayers.TryGetValue(guildId, out var player))
                {
                    //Program.Logger.Debug("Reusing AudioPlayerInstace.");
                    return player;
                }
                else
                {
                    //Program.Logger.Debug("Player connected, but no AudioPlayerInstace found.");
                    var audioClient = await channel.ConnectAsync(true);
                    var audioPlayer = new AudioPlayer(channel, audioClient);

                    AudioPlayers.Add(guildId, audioPlayer);

                    return audioPlayer;
                }
            }
            else
            {
                AudioPlayers.Remove(guildId);

                //Program.Logger.Debug("Creating new AudioPlayerInstace.");
                var audioClient = await channel.ConnectAsync(true);
                var audioPlayer = new AudioPlayer(channel, audioClient);

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
                if (player != null)
                {
                    await player.Stop();
                    await player.AudioChannel.DisconnectAsync();
                }
                player?.Dispose();
                player = null;
                AudioPlayers.Remove(guildId);
            }
        }

        private async Task<bool> IsAudioPlayerConnected(IVoiceChannel channel)
        {
            return await channel.GetUserAsync(_client.CurrentUser.Id) != null;
        }
    }
}
