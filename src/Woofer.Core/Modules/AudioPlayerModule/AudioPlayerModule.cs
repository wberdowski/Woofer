using Discord;
using Discord.Interactions;
using Woofer.Core.Extensions;
using Woofer.Core.Modules.Common.Enums;

namespace Woofer.Core.Modules.AudioPlayerModule
{
    public class AudioPlayerModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly SearchProvider _searchProvider;
        private readonly AudioPlayerManager _audioPlayerManager;
        public static PlayMessageInfo? _lastPlayMessage { get; set; }

        public AudioPlayerModule(AudioPlayerManager audioPlayerManager, SearchProvider searchProvider)
        {
            _audioPlayerManager = audioPlayerManager;
            _searchProvider = searchProvider;
        }

        [ComponentInteraction("play-now-button")]
        public async Task HandlePlayNowButton()
        {
            var component = (IComponentInteraction)Context.Interaction;

            if (_lastPlayMessage == null)
            {
                return;
            }

            if (!TryGetActivePlayer(component, out var audioPlayer))
            {
                await component.RespondWithUserError(UserError.NoAudioPlayerFound);
                return;
            }

            if (await audioPlayer!.TryPlayNow(_lastPlayMessage.Track))
            {
                _lastPlayMessage?.TryRevokeControls().Wait();
            }

            await component.DeferAsync();
        }

        [ComponentInteraction("play-next-button")]
        public async Task HandlePlayNextButton()
        {
            var component = (IComponentInteraction)Context.Interaction;

            if (_lastPlayMessage == null)
            {
                return;
            }

            if (!TryGetActivePlayer(component, out var audioPlayer))
            {
                await component.RespondWithUserError(UserError.NoAudioPlayerFound);
                return;
            }

            if (await audioPlayer!.TryEnqueueNext(_lastPlayMessage.Track))
            {
                _lastPlayMessage?.TryRevokeControls().Wait();
            }

            await component.DeferAsync();
        }

        [ComponentInteraction("delete-button")]
        public async Task HandleDeleteButton()
        {
            var component = (IComponentInteraction)Context.Interaction;

            if (_lastPlayMessage == null)
            {
                return;
            }

            if (!TryGetActivePlayer(component, out var audioPlayer))
            {
                await component.RespondWithUserError(UserError.NoAudioPlayerFound);
                return;
            }

            await audioPlayer!.Delete(_lastPlayMessage.Track);
            await _lastPlayMessage.Interaction.DeleteOriginalResponseAsync();
        }

        [SlashCommand("play", "Play an audio track from YouTube")]
        [RequireContext(ContextType.Guild)]
        public async Task HandlePlayCommand(string songOrUrl)
        {
            var command = (ISlashCommandInteraction)Context.Interaction;
            var channel = (command.User as IGuildUser)?.VoiceChannel;

            if (channel == null)
            {
                await command.RespondWithUserError(UserError.UserNotInTheChannel);
                return;
            }

            _lastPlayMessage?.TryRevokeControls().Wait();

            {
                var embed = new EmbedBuilder()
                   .WithDescription($"🔍 Searching YouTube: \"**{songOrUrl}**\"")
                   .WithColor(Color.DarkPurple)
                   .Build();

                await command.RespondAsync(
                    embeds: new Embed[] { embed },
                    ephemeral: true
                );
            }

            var track = await _searchProvider.Search(songOrUrl);

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

            var audioPlayer = await _audioPlayerManager.RequestAudioPlayerAtChannel((ulong)command.GuildId!, channel);
            await audioPlayer!.Enqueue(track);
        }

        [SlashCommand("delete", "Delete track at the given position")]
        [RequireContext(ContextType.Guild)]
        public async Task HandleDeleteCommand(int index)
        {
            var command = (ISlashCommandInteraction)Context.Interaction;

            if (!TryGetActivePlayer(command, out var audioPlayer))
            {
                await command.RespondWithUserError(UserError.NoAudioPlayerFound);
                return;
            }

            if (index < 1)
            {
                await command.RespondWithUserError(UserError.InvalidTrackPosition);
                return;
            }

            var track = audioPlayer!.TrackQueue.ElementAtOrDefault(index - 1);

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

        [SlashCommand("stop", "Stop currently playing track")]
        [RequireContext(ContextType.Guild)]
        public async Task HandleStopCommand()
        {
            var command = (ISlashCommandInteraction)Context.Interaction;

            _lastPlayMessage?.TryRevokeControls().Wait();

            if (!TryGetActivePlayer(command, out var audioPlayer))
            {
                await command.RespondWithUserError(UserError.NoAudioPlayerFound);
                return;
            }

            var track = await audioPlayer!.Stop();

            var embed = new EmbedBuilder()
               .WithDescription($"⏹️ Stopping \"**{track?.Title}**\"")
               .WithColor(Color.DarkPurple)
               .Build();

            await command.RespondAsync(
                embeds: new Embed[] { embed }
            );
        }

        [SlashCommand("skip", "Skip current track and play next in the queue")]
        [RequireContext(ContextType.Guild)]
        public async Task HandleSkipCommand()
        {
            var command = (ISlashCommandInteraction)Context.Interaction;

            _lastPlayMessage?.TryRevokeControls().Wait();

            if (!TryGetActivePlayer(command, out var audioPlayer))
            {
                await command.RespondWithUserError(UserError.NoAudioPlayerFound);
                return;
            }

            var track = await audioPlayer!.Skip();

            if (track == null)
            {
                await command.RespondWithUserError(UserError.NoTrackIsCurrentlyPlaying);
                return;
            }

            var embed = new EmbedBuilder()
               .WithDescription($"⏭️ Skipping \"**{track?.Title}**\"")
               .WithColor(Color.DarkPurple)
               .Build();

            await command.RespondAsync(
                embeds: new Embed[] { embed }
            );
        }

        [SlashCommand("pause", "Pause currently playing track")]
        [RequireContext(ContextType.Guild)]
        public async Task HandlePauseCommand()
        {
            var command = (ISlashCommandInteraction)Context.Interaction;

            _lastPlayMessage?.TryRevokeControls().Wait();

            if (!TryGetActivePlayer(command, out var audioPlayer))
            {
                await command.RespondWithUserError(UserError.NoAudioPlayerFound);
                return;
            }

            var track = await audioPlayer!.Pause();

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

        [SlashCommand("resume", "Resume currently playing track")]
        [RequireContext(ContextType.Guild)]
        public async Task HandleResumeCommand()
        {
            var command = (ISlashCommandInteraction)Context.Interaction;

            _lastPlayMessage?.TryRevokeControls().Wait();

            if (!TryGetActivePlayer(command, out var audioPlayer))
            {
                await command.RespondWithUserError(UserError.NoAudioPlayerFound);
                return;
            }

            var track = await audioPlayer!.Unpause();

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

        [SlashCommand("queue", "Show currently playing track and the queue")]
        [RequireContext(ContextType.Guild)]
        public async Task HandleQueueCommand()
        {
            var command = (ISlashCommandInteraction)Context.Interaction;

            if (!TryGetActivePlayer(command, out var audioPlayer))
            {
                await command.RespondWithUserError(UserError.NoAudioPlayerFound);
                return;
            }

            var totalDuration = TimeSpan.FromSeconds(
                (audioPlayer!.CurrentTrack?.Duration?.TotalSeconds ?? 0) +
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

        [SlashCommand("leave", "Stop currently playing track and leave the channel")]
        [RequireContext(ContextType.Guild)]
        public async Task HandleLeaveCommand()
        {
            var command = (ISlashCommandInteraction)Context.Interaction;

            _lastPlayMessage?.TryRevokeControls().Wait();

            if (!TryGetActivePlayer(command, out var audioPlayer))
            {
                await command.RespondWithUserError(UserError.NoAudioPlayerFound);
                return;
            }

            _audioPlayerManager.DisconnectAudioPlayer(audioPlayer);

            var embed = new EmbedBuilder()
               .WithDescription($"👋 Bye!")
               .WithColor(Color.DarkPurple)
               .Build();

            await command.RespondAsync(
                embeds: new Embed[] { embed }
            );
        }

        private bool TryGetActivePlayer(IDiscordInteraction context, out AudioPlayer? audioPlayer)
        {
            if (context.GuildId == null)
            {
                audioPlayer = null;
                return false;
            }

            audioPlayer = _audioPlayerManager.GetAudioPlayer((ulong)context.GuildId);

            return audioPlayer != null;
        }
    }
}
