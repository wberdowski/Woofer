using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Woofer.Core.Attributes;
using Woofer.Core.Common;
using Woofer.Core.Modules.Common.Enums;
using Woofer.Core.Modules.Common.Extensions;

namespace Woofer.Core.Modules.AudioPlayerModule
{
    internal class AudioPlayerModule : AppModule<AudioPlayerModule>
    {
        private readonly SearchProvider _searchProvider;
        private readonly AudioPlayerManager _audioPlayerManager;
        private PlayMessageInfo? _lastPlayMessage;

        public AudioPlayerModule(ILogger<AudioPlayerModule> logger, AudioPlayerManager audioPlayerManager, SearchProvider searchProvider) : base(logger)
        {
            _audioPlayerManager = audioPlayerManager;
            _searchProvider = searchProvider;
        }

        public override IEnumerable<ApplicationCommandProperties> RegisterCommands()
        {
            RegisterCommand("play", "Play an audio track from YouTube.", HandlePlayCommand, new SlashCommandBuilder()
                .AddOption("song-title-or-url", ApplicationCommandOptionType.String, "Title or url of the YouTube video.", isRequired: true));

            RegisterCommand("delete", "Delete track at the provided position.", HandleDeleteCommand, new SlashCommandBuilder()
                .AddOption("track-position", ApplicationCommandOptionType.Integer, "Track position in the queue.", isRequired: true));

            RegisterCommand("stop", "Stop currently playing track.", HandleStopCommand);
            RegisterCommand("skip", "Skip current track and play next in the queue.", HandleSkipCommand, "next");
            RegisterCommand("queue", "Show currently playing track and the queue.", HandleQueueCommand, "current", "nowplaying");
            RegisterCommand("pause", "Pause currently playing track.", HandlePauseCommand);
            RegisterCommand("resume", "Resume currently playing track.", HandleResumeCommand);

            return base.RegisterCommands();
        }

        public override async Task HandleButtonExecuted(SocketMessageComponent component)
        {
            if (!TryGetActivePlayer(component, out var audioPlayer))
            {
                await SendNoActivePlayerError(component);
                return;
            }

            if (_lastPlayMessage == null)
            {
                return;
            }

            switch (component.Data.CustomId)
            {
                case "play-now-button":
                    if (await audioPlayer.TryPlayNow(_lastPlayMessage.Track))
                    {
                        _lastPlayMessage?.TryRevokeControls().Wait();
                        break;
                    }

                    await component.DeferAsync();

                    break;

                case "play-next-button":
                    if (await audioPlayer.TryEnqueueNext(_lastPlayMessage.Track))
                    {
                        _lastPlayMessage?.TryRevokeControls().Wait();
                        break;
                    }

                    await component.DeferAsync();

                    break;

                case "delete-button":
                    await audioPlayer.Delete(_lastPlayMessage.Track);
                    await _lastPlayMessage.Interaction.DeleteOriginalResponseAsync();
                    break;
            }
        }

        [RequireGuild]
        private async Task HandlePlayCommand(SocketSlashCommand command)
        {
            var channel = (command.User as IGuildUser)?.VoiceChannel;

            if (channel == null)
            {
                await command.RespondWithUserError(UserError.UserNotInTheChannel);
                return;
            }

            _lastPlayMessage?.TryRevokeControls().Wait();

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

            var track = await _searchProvider.Search(searchQuery);

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

                _lastPlayMessage = new PlayMessageInfo(command, embed, track);
            }

            var audioPlayer = await _audioPlayerManager.RequestAudioPlayerAtChannel((ulong)command.GuildId, channel);
            await audioPlayer.Enqueue(track);
        }

        private async Task HandleDeleteCommand(SocketSlashCommand command)
        {
            if (!TryGetActivePlayer(command, out var audioPlayer))
            {
                await SendNoActivePlayerError(command);
                return;
            }

            var index = command.Data.Options.FirstOrDefault()?.Value as long?;

            if (index == null || index < 1)
            {
                await command.RespondWithUserError(UserError.InvalidTrackPosition);
                return;
            }

            var track = audioPlayer.TrackQueue.ElementAtOrDefault((int)index - 1);

            if (track == null)
            {
                await command.RespondWithUserError(UserError.TrackNotFound);
                return;
            }

            await audioPlayer.Delete(track);

            var embed = new EmbedBuilder()
               .WithDescription($"🗑 Deleting \"**{track?.Title}**\"")
               .WithColor(Color.DarkPurple)
               .Build();

            await command.RespondAsync(
                embeds: new Embed[] { embed }
            );
        }


        private async Task HandleStopCommand(SocketSlashCommand command)
        {
            _lastPlayMessage?.TryRevokeControls().Wait();

            if (!TryGetActivePlayer(command, out var audioPlayer))
            {
                await SendNoActivePlayerError(command);
                return;
            }

            var track = await audioPlayer.Stop();

            var embed = new EmbedBuilder()
               .WithDescription($"⏹️ Stopping \"**{track?.Title}**\"")
               .WithColor(Color.DarkPurple)
               .Build();

            await command.RespondAsync(
                embeds: new Embed[] { embed }
            );
        }

        private async Task HandleSkipCommand(SocketSlashCommand command)
        {
            _lastPlayMessage?.TryRevokeControls().Wait();

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
            _lastPlayMessage?.TryRevokeControls().Wait();

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
            _lastPlayMessage?.TryRevokeControls().Wait();

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
                return $"> *{i + 1}* - ``{song.Duration}`` - **{song.Title}**\n";
            }));

            if (!audioPlayer.TrackQueue.Any())
            {
                queue = "No tracks in the queue.";
            }

            var embedBuilder = new EmbedBuilder()
                .WithAuthor($"🎵 Now playing:")
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
                    .WithTitle($"Nothing is currently playing 😴");
            }

            var embed = embedBuilder.Build();

            await command.RespondAsync(
                embeds: new Embed[] { embed },
                ephemeral: true
            );
        }

        private bool TryGetActivePlayer(IDiscordInteraction context, out AudioPlayer? audioPlayer)
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
    }
}
