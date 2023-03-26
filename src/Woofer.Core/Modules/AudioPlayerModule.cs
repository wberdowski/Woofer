using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using System.Reflection.Emit;
using Woofer.Core.Audio;
using Woofer.Core.Common;
using Woofer.Core.Common.Enums;
using Woofer.Core.Modules.Extensions;
using YoutubeExplode;

namespace Woofer.Core.Modules
{
    internal class AudioPlayerModule : AppModule
    {
        private readonly YoutubeClient _ytClient;
        private readonly AudioPlayerManager _audioPlayerManager;
        private readonly ILogger _logger;
        private SocketSlashCommand? _lastAddToQueueMessage;
        private Embed? _lastAddToQueueMessageEmbed;
        private Track? _lastAddToQueueMessageTrack;

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
                    .WithDescription("Show currently playing song and the queue.")
                    .Build(),

                new SlashCommandBuilder()
                    .WithName("current")
                    .WithDescription("Show currently playing song and the queue.")
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

        public override async Task HandleCommand(SocketSlashCommand command)
        {
            var task = command.CommandName switch
            {
                "play" or "p" => HandlePlayCommand(command),
                "stop" => HandleStopCommand(command),
                "skip" or "next" => HandleSkipCommand(command),
                "queue" or "current" => HandleQueueCommand(command),
                "pause" => HandlePauseCommand(command),
                "resume" => HandleResumeCommand(command),
                _ => Task.CompletedTask
            };
        }

        public override async Task HandleButtonExecuted(SocketMessageComponent component)
        {
            // TODO

            if (!TryGetActivePlayer(component, out var audioPlayer))
            {
                await SendNoActivePlayerError(component);
                return;
            }

            if (_lastAddToQueueMessage == null || _lastAddToQueueMessageEmbed == null || _lastAddToQueueMessageTrack == null)
            {
                return;
            }

            switch (component.Data.CustomId)
            {
                case "play-now-button":
                    if (await audioPlayer.TryPlayNow(_lastAddToQueueMessageTrack))
                    {
                        await TryRevokeLastMessageControls();
                        break;
                    }

                    await component.DeferAsync();

                    break;

                case "play-next-button":
                    if (await audioPlayer.TryEnqueueNext(_lastAddToQueueMessageTrack))
                    {
                        await TryRevokeLastMessageControls();
                        break;
                    }

                    await component.DeferAsync();

                    break;

                case "delete-button":
                    await audioPlayer.Delete(_lastAddToQueueMessageTrack);
                    await TryRevokeLastMessageControls();
                    break;
            }
        }

        private async Task HandlePlayCommand(SocketSlashCommand command)
        {
            await TryRevokeLastMessageControls();

            if (command.GuildId == null)
            {
                return;
            }

            var searchQuery = command.Data.Options.First().Value.ToString();

            {
                var embed = new EmbedBuilder()
                   .WithDescription($"🔍 Searching YouTube: \"**{searchQuery}**\"")
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
                    .WithFooter($"{track.Id}")
                    .WithThumbnailUrl(track.ThumbnailUrl)
                    .WithColor(Color.DarkPurple)
                    .Build();

                var components = new ComponentBuilder()
                    .WithButton("Now", $"play-now-button", ButtonStyle.Primary)
                    .WithButton("Next", $"play-next-button", ButtonStyle.Secondary)
                    .WithButton("❌", $"delete-button", ButtonStyle.Danger)
                    .Build();

                await command.ModifyOriginalResponseAsync((p) =>
                {
                    p.Embed = embed;
                    p.Components = components;
                });

                _lastAddToQueueMessage = command;
                _lastAddToQueueMessageEmbed = embed;
                _lastAddToQueueMessageTrack = track;
            }

            var channel = (command.User as IGuildUser)?.VoiceChannel;
            if (channel == null)
            {
                await command.RespondAsync("User not in the channel.");
                return;
            }

            var audioPlayer = await _audioPlayerManager.RequestAudioPlayerAtChannel((ulong)command.GuildId, channel);
            await audioPlayer.Enqueue(track);
        }

        private async Task HandleStopCommand(SocketSlashCommand command)
        {
            await TryRevokeLastMessageControls();

            if (!TryGetActivePlayer(command, out var audioPlayer))
            {
                await SendNoActivePlayerError(command);
                return;
            }

            var track = await audioPlayer.Stop();

            {
                var embed = new EmbedBuilder()
                   .WithDescription($"⏹️ Stopping \"**{track?.Title}**\"")
                   .WithColor(Color.DarkPurple)
                   .Build();

                await command.RespondAsync(
                    embeds: new Embed[] { embed }
                );
            }
        }

        private async Task HandleSkipCommand(SocketSlashCommand command)
        {
            await TryRevokeLastMessageControls();

            if (!TryGetActivePlayer(command, out var audioPlayer))
            {
                await SendNoActivePlayerError(command);
                return;
            }

            var track = await audioPlayer.Skip();

            var embed = new EmbedBuilder()
               .WithDescription($"⏭️ Skipping \"**{track?.Title}**\"")
               .WithColor(Color.DarkPurple)
               .Build();

            await command.RespondAsync(
                embeds: new Embed[] { embed }
            );
        }

        private async Task HandlePauseCommand(SocketSlashCommand command)
        {
            await TryRevokeLastMessageControls();

            if (!TryGetActivePlayer(command, out var audioPlayer))
            {
                await SendNoActivePlayerError(command);
                return;
            }

            var track = await audioPlayer.Pause();

            if (track == null)
            {
                await command.RespondWithUserError(UserError.NoTrackIsCurrentlyPlaying);
                return;
            }

            var embed = new EmbedBuilder()
               .WithDescription($"⏸️ Pausing \"**{track?.Title}**\"")
               .WithColor(Color.DarkPurple)
               .Build();

            await command.RespondAsync(
                embed: embed
            );
        }

        private async Task HandleResumeCommand(SocketSlashCommand command)
        {
            await TryRevokeLastMessageControls();

            if (!TryGetActivePlayer(command, out var audioPlayer))
            {
                await SendNoActivePlayerError(command);
                return;
            }

            var track = await audioPlayer.Unpause();

            if (track == null)
            {
                await command.RespondWithUserError(UserError.NoTrackIsCurrentlyPlaying);
                return;
            }

            var embedBuilder = new EmbedBuilder()
                .WithDescription($"▶️ Resuming \"**{track?.Title}**\"")
                .WithColor(Color.DarkPurple);

            await command.RespondAsync(
                embed: embedBuilder.Build()
            );
        }

        private async Task HandleQueueCommand(SocketSlashCommand command)
        {
            if (!TryGetActivePlayer(command, out var audioPlayer))
            {
                await SendNoActivePlayerError(command);
                return;
            }

            var totalDuration = TimeSpan.FromSeconds(
                (audioPlayer.CurrentTrack?.Duration?.TotalSeconds ?? 0) +
                audioPlayer.TrackQueue.Sum(s => s.Duration?.TotalSeconds ?? 0)
            );

            var queue = string.Join("\n", audioPlayer.TrackQueue.Select((song, i) =>
            {
                return $"{i + 1} - {song.Title} {song.Duration} - {song.AudioSource.Codec} {song.AudioSource.Bitrate} kbps - {song.Id}``";
            }));

            var embedBuilder = new EmbedBuilder()
                .WithAuthor($"🎵 NOW PLAYING")
                .WithDescription($"**☰ Queue**\n{queue}")
                .WithColor(Color.DarkPurple)
                .WithFooter($"Total duration: {totalDuration}");

            if (audioPlayer.CurrentTrack != null)
            {
                embedBuilder = embedBuilder
                    .WithTitle($"{audioPlayer.CurrentTrack.Title}")
                    .WithThumbnailUrl(audioPlayer.CurrentTrack.ThumbnailUrl)
                    .WithUrl(audioPlayer.CurrentTrack.Url);
            }
            else
            {
                embedBuilder = embedBuilder
                    .WithTitle($"❌ No track is currently playing");
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
                   .WithAuthor($"❌ No track is currently playing.")
                   .WithColor(Color.Red)
                   .Build();

            await context.RespondAsync(
                embeds: new Embed[] { embed },
                ephemeral: true
            );
        }

        private async Task<bool> TryRevokeLastMessageControls()
        {
            if (_lastAddToQueueMessage != null && _lastAddToQueueMessageEmbed != null && _lastAddToQueueMessageTrack != null)
            {
                await _lastAddToQueueMessage.ModifyOriginalResponseAsync(m =>
                {
                    m.Embed = _lastAddToQueueMessageEmbed;
                    m.Components = null;
                });

                _lastAddToQueueMessage = null;
                _lastAddToQueueMessageEmbed = null;
                _lastAddToQueueMessageTrack = null;

                return true;
            }

            return false;
        }
    }
}
