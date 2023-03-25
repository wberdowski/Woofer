using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Woofer.Core.Audio;
using Woofer.Core.Common;
using YoutubeExplode;

namespace Woofer.Core.Modules
{
    internal class AudioPlayerModule : AppModule
    {
        private readonly YoutubeClient _ytClient;
        private readonly AudioPlayerManager _audioPlayerManager;
        private readonly ILogger _logger;
        private Guid? _lastMessageId;

        public AudioPlayerModule(YoutubeClient client, AudioPlayerManager audioPlayerManager, ILogger<AudioPlayerModule> logger)
        {
            _ytClient = client;
            _audioPlayerManager = audioPlayerManager;
            _logger = logger;
        }

        public override IEnumerable<ApplicationCommandProperties> RegisterCommands()
        {
            var properties = new List<ApplicationCommandProperties>
            {
                new SlashCommandBuilder()
                    .WithName("play")
                    .WithDescription("Play an audio track from YouTube.")
                    .AddOption("song-title-or-url", ApplicationCommandOptionType.String, "Title or url of the YouTube video.", isRequired: true)
                    .Build(),

                new SlashCommandBuilder()
                    .WithName("stop")
                    .WithDescription("Stop currently playing song.")
                    .Build(),

                new SlashCommandBuilder()
                    .WithName("skip")
                    .WithDescription("Skip current song and play next in the queue.")
                    .Build(),

                new SlashCommandBuilder()
                    .WithName("next")
                    .WithDescription("Skip current song and play next in the queue.")
                    .Build(),

                new SlashCommandBuilder()
                    .WithName("queue")
                    .WithDescription("Show songs in the queue.")
                    .Build(),

                new SlashCommandBuilder()
                    .WithName("pause")
                    .WithDescription("Pause currently playing song.")
                    .Build(),

                new SlashCommandBuilder()
                    .WithName("resume")
                    .WithDescription("Resume currently playing song.")
                    .Build()
            };

            return properties;
        }

        public override Task HandleCommand(SocketSlashCommand command)
        {
            var task = command.CommandName switch
            {
                "play" => HandlePlayCommand(command),
                "stop" => HandleStopCommand(command),
                "skip" or "next" => HandleSkipCommand(command),
                "queue" => HandleQueueCommand(command),
                "pause" => HandlePauseCommand(command),
                "resume" => HandleResumeCommand(command),
                _ => Task.CompletedTask
            };

            return task;
        }

        public override async Task HandleButtonExecuted(SocketMessageComponent component)
        {
            if (component.Data.CustomId == $"{_lastMessageId}-play-now-button")
            {
                await component.DeferAsync();
            }
        }

        private async Task HandlePlayCommand(SocketSlashCommand command)
        {
            if (command.GuildId == null)
            {
                return;
            }

            var messageId = Guid.NewGuid();
            var searchQuery = command.Data.Options.First().Value.ToString();

            {
                var embed = new EmbedBuilder()
                   .WithAuthor($"🔍 Searching YouTube...")
                   .WithDescription($"Search phrase: \"**{searchQuery}**\"")
                   .WithColor(Color.DarkPurple)
                   .Build();

                await command.RespondAsync(
                   embeds: new Embed[] { embed },
                   ephemeral: true
               );
            }

            var results = _ytClient.Search.GetResultsAsync(searchQuery);
            var result = results.FirstAsync().Result;
            var video = await _ytClient.Videos.GetAsync(result.Url);
            var manifest = await _ytClient.Videos.Streams.GetManifestAsync(result.Url);

            // Select best quality
            var audioSource = manifest.GetAudioOnlyStreams()
                .Where(x => x.AudioCodec == "opus")
                .OrderByDescending(x => x.Bitrate.KiloBitsPerSecond)
                .First();

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
                    .WithButton("Now", $"{messageId}-play-now-button", ButtonStyle.Primary)
                    .WithButton("Next", $"{messageId}-play-next-button", ButtonStyle.Secondary)
                    .WithButton("❌", $"{messageId}-delete", ButtonStyle.Danger)
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
                return;
            }

            var audioPlayer = await _audioPlayerManager.RequestAudioPlayerAtChannel((ulong)command.GuildId, channel);
            audioPlayer.Enqueue(track);

            _lastMessageId = messageId;
        }

        private async Task HandleStopCommand(SocketSlashCommand command)
        {
            if (!TryGetActivePlayer(command, out var audioPlayer))
            {
                await SendNoActivePlayerError(command);
                return;
            }

            var stoppedTrack = await audioPlayer.Stop();

            {
                var embed = new EmbedBuilder()
                   .WithAuthor($"⏹️ Stopping {stoppedTrack?.Title}")
                   .WithColor(Color.DarkPurple)
                   .Build();

                await command.RespondAsync(
                    embeds: new Embed[] { embed }
                );
            }
        }

        private async Task HandleSkipCommand(SocketSlashCommand command)
        {
            if (!TryGetActivePlayer(command, out var audioPlayer))
            {
                await SendNoActivePlayerError(command);
                return;
            }

            var skippedTrack = await audioPlayer.Skip();

            var embed = new EmbedBuilder()
               .WithAuthor($"⏭️ Skipping {skippedTrack?.Title}")
               .WithColor(Color.DarkPurple)
               .Build();

            await command.RespondAsync(
                embeds: new Embed[] { embed }
            );
        }

        private async Task HandlePauseCommand(SocketSlashCommand command)
        {
            if (!TryGetActivePlayer(command, out var audioPlayer))
            {
                await SendNoActivePlayerError(command);
                return;
            }

            audioPlayer.Pause();

            var embed = new EmbedBuilder()
               .WithAuthor($"⏸️ Pausing")
               .WithColor(Color.DarkPurple)
               .Build();

            await command.RespondAsync(
                embeds: new Embed[] { embed }
            );
        }

        private async Task HandleResumeCommand(SocketSlashCommand command)
        {
            if (!TryGetActivePlayer(command, out var audioPlayer))
            {
                await SendNoActivePlayerError(command);
                return;
            }

            audioPlayer.Unpause();

            var embed = new EmbedBuilder()
               .WithAuthor($"▶️ Resuming")
               .WithColor(Color.DarkPurple)
               .Build();

            await command.RespondAsync(
                embeds: new Embed[] { embed }
            );
        }

        private async Task HandleQueueCommand(SocketSlashCommand command)
        {
            if (!TryGetActivePlayer(command, out var audioPlayer))
            {
                await SendNoActivePlayerError(command);
                return;
            }

            var songsList = string.Join("\n", audioPlayer.TrackQueue.Select((song, i) => $"**{i + 1}** - {song.Title} - [{song.Duration}]"));
            var totalDuration = TimeSpan.FromSeconds(
                (audioPlayer.CurrentTrack?.Duration?.TotalSeconds ?? 0) +
                audioPlayer.TrackQueue.Sum(s => s.Duration?.TotalSeconds ?? 0)
            );

            var embedBuilder = new EmbedBuilder()
                .WithAuthor($"☰ Queue")
                .WithDescription($"{songsList}")
                .WithColor(Color.DarkPurple)
                .WithFooter($"Total duration: {totalDuration}");

            if (audioPlayer.CurrentTrack != null)
            {
                embedBuilder = embedBuilder
                    .WithTitle($"**🎵 NOW PLAYING**\n```{audioPlayer.CurrentTrack.Title} [{audioPlayer.CurrentTrack.Duration}]```")
                    .WithUrl(audioPlayer.CurrentTrack.Url);
            }
            else
            {
                embedBuilder = embedBuilder
                    .WithTitle($"❌ No song is currently playing");
            }

            var embed = embedBuilder.Build();

            await command.RespondAsync(
                embeds: new Embed[] { embed },
                ephemeral: true
            );
        }

        private bool TryGetActivePlayer(IDiscordInteraction context, out AudioPlayer audioPlayer)
        {
            audioPlayer = _audioPlayerManager.GetAudioPlayer((ulong)context.GuildId);

            return audioPlayer != null;
        }

        private async Task SendNoActivePlayerError(IDiscordInteraction context)
        {
            var embed = new EmbedBuilder()
                   .WithAuthor($"❌ No music is currently playing.")
                   .WithColor(Color.Red)
                   .Build();

            await context.RespondAsync(
                embeds: new Embed[] { embed },
                ephemeral: true
            );
        }
    }
}
