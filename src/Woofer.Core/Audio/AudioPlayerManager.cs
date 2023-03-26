using Discord;
using Discord.WebSocket;
using ManagedBass;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Woofer.Core.Audio
{
    internal class AudioPlayerManager
    {
        private readonly Dictionary<ulong, AudioPlayer> _audioPlayers;
        private readonly DiscordSocketClient _client;
        private readonly ILogger<AudioPlayerManager> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IServiceScope _scope;

        public AudioPlayerManager(DiscordSocketClient client, ILogger<AudioPlayerManager> logger, IServiceProvider serviceProvider)
        {
            _audioPlayers = new Dictionary<ulong, AudioPlayer>();
            _client = client;
            _logger = logger;

            if (!Bass.Init(0, -1, DeviceInitFlags.Default))
            {
                throw new Exceptions.BassException("Can't initialize BASS library.");
            }

            Bass.PluginLoad("bassopus");
            Bass.PluginLoad("basswebm");

            if (Bass.LastError != Errors.OK)
            {
                throw new Exceptions.BassException(Bass.LastError);
            }

            _serviceProvider = serviceProvider;
            _scope = _serviceProvider.CreateScope();
        }

        public async Task<AudioPlayer?> RequestAudioPlayerAtChannel(ulong guildId, IVoiceChannel channel)
        {
            AudioPlayer audioPlayer;

            if (!_audioPlayers.TryGetValue(guildId, out audioPlayer))
            {
                audioPlayer = _scope.ServiceProvider.GetRequiredService<AudioPlayer>();
                var audioClient = await channel.ConnectAsync(true);
                await audioPlayer.SetAudioClient(audioClient);
                _audioPlayers.Add(guildId, audioPlayer);

                return audioPlayer;
            }

            if (!await IsAudioPlayerConnected(channel))
            {
                var audioClient = await channel.ConnectAsync(true);
                await audioPlayer.SetAudioClient(audioClient);
            }

            return audioPlayer;
        }

        public AudioPlayer? GetAudioPlayer(ulong guildId)
        {
            if (_audioPlayers.TryGetValue(guildId, out var player))
            {
                return player;
            }

            return null;
        }

        public async Task DisposeAudioPlayer(ulong guildId)
        {
            if (_audioPlayers.TryGetValue(guildId, out var player))
            {
                player?.Dispose();
                _audioPlayers.Remove(guildId);
            }
        }

        private async Task<bool> IsAudioPlayerConnected(IVoiceChannel channel)
        {
            return await channel.GetUserAsync(_client.CurrentUser.Id) != null;
        }

        ~AudioPlayerManager()
        {
            Bass.Free();
        }
    }
}
