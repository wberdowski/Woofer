using Discord;
using Discord.WebSocket;
using Woofer.Core.Audio;
using Woofer.Core.Common;
using YoutubeExplode;

namespace Woofer.Core.Modules
{
    internal class AudioPlayerModule : AppModule
    {
        private readonly YoutubeClient _ytClient;
        private readonly AudioPlayerManager _audioPlayerManager;
        private Guid _lastMessageId;

        public AudioPlayerModule(YoutubeClient client, AudioPlayerManager audioPlayerManager)
        {
            _ytClient = client;
            _audioPlayerManager = audioPlayerManager;
        }

        public override IEnumerable<ApplicationCommandProperties> RegisterCommands()
        {
            var properties = new List<ApplicationCommandProperties>();

            var cmd = new SlashCommandBuilder()
                .WithName("p")
                .WithDescription("Play an audio track from YouTube.")
                .AddOption("song-title-or-url", ApplicationCommandOptionType.String, "Title or url of the YouTube video.", isRequired: true);

            properties.Add(cmd.Build());

            return properties;
        }

        public override async Task HandleCommand(SocketSlashCommand command)
        {
            if (command.CommandName == "p")
            {
                var messageId = Guid.NewGuid();
                var searchQuery = command.Data.Options.First().Value.ToString();

                {
                    var embed = new EmbedBuilder()
                       .WithAuthor($"Searching YouTube for {searchQuery}...")
                       .WithColor(Color.DarkPurple)
                       .Build();

                    await command.RespondAsync(
                       embeds: new Embed[] { embed }
                   );
                }

                var results = _ytClient.Search.GetResultsAsync(searchQuery);
                var result = results.FirstAsync().Result;
                var video = await _ytClient.Videos.GetAsync(result.Url);
                var manifest = await _ytClient.Videos.Streams.GetManifestAsync(result.Url);

                // Select best quality
                var audioSource = manifest.GetAudioOnlyStreams().Where(x => x.AudioCodec == "opus").OrderByDescending(x => x.Bitrate.KiloBitsPerSecond).First();

                var track = new WebSearchResult(
                    video.Title,
                    video.Duration,
                    TimeSpan.Zero,
                    video.Url,
                    video.Thumbnails.FirstOrDefault()?.Url,
                    new WebAudioSource(
                        (int)Math.Ceiling(audioSource.Bitrate.KiloBitsPerSecond),
                        audioSource.AudioCodec,
                        audioSource.Url
                    )
                );

                {
                    var embed = new EmbedBuilder()
                        .WithAuthor("➕ Song added to queue")
                        .WithTitle(track.Title)
                        .WithUrl(track.Url)
                        .WithDescription($"**{track.Duration?.ToString()}** | {track.AudioSource.Codec} {track.AudioSource.Bitrate} kbps")
                        .WithThumbnailUrl(track.ThumbnailUrl)
                        .WithColor(Color.DarkPurple)
                        .Build();

                    var components = new ComponentBuilder()
                        .WithButton("⏭️ Now", $"{messageId}-play-now-button", ButtonStyle.Primary)
                        .WithButton("➡️ Next", $"{messageId}-play-next-button", ButtonStyle.Secondary)
                        .WithButton("❌ Delete", $"{messageId}-delete", ButtonStyle.Danger)
                        .Build();

                    await command.ModifyOriginalResponseAsync((p) =>
                    {
                        p.Embed = embed;
                        p.Components = components;
                    });
                }

                var channel = (command.User as IGuildUser)?.VoiceChannel;
                if (channel == null)
                {
                    await command.RespondAsync("User not in the channel.");
                    //await BasicCommandFailureReply("User must be in a voice channel, or a voice channel must be passed as an argument.", "Error");
                    return;
                }

                // Join voice channel
                var audioPlayerTask = _audioPlayerManager.RequestAudioPlayerAtChannel((ulong)command.GuildId, channel);

                var audioPlayer = await audioPlayerTask;

                await audioPlayer.Enqueue(track);

                _lastMessageId = messageId;
            }
        }

        public override async Task HandleButtonExecuted(SocketMessageComponent component)
        {
            if (component.Data.CustomId == $"{_lastMessageId}-play-now-button")
            {
                //await component.RespondAsync("oki doki");
                await component.DeferAsync();
            }
        }
    }
}
