using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Woofer.Core.Audio;
using Woofer.Core.Common;
using Woofer.Core.Common.Enums;
using Woofer.Core.Modules.Attributes;
using Woofer.Core.Modules.Extensions;

namespace Woofer.Core.Modules
{
    internal class AudioPlayerModule : AppModule
    {
        private readonly SearchProvider _searchProvider;
        private readonly AudioPlayerManager _audioPlayerManager;
        private readonly ILogger _logger;
        private SocketSlashCommand? _lastAddToQueueMessage;
        private Embed? _lastAddToQueueMessageEmbed;
        private Track? _lastAddToQueueMessageTrack;

        public AudioPlayerModule(AudioPlayerManager audioPlayerManager, ILogger<AudioPlayerModule> logger, SearchProvider searchProvider)
        {
            _audioPlayerManager = audioPlayerManager;
            _logger = logger;
            _searchProvider = searchProvider;
        }

        public override Task<IEnumerable<ApplicationCommandProperties>> RegisterCommands()
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

        [RequireGuild]
        private async Task HandlePlayCommand(SocketSlashCommand command)
        {
            var channel = (command.User as IGuildUser)?.VoiceChannel;

            if (channel == null)
            {
                await command.RespondWithUserError(UserError.UserNotInTheChannel);
                return;
            }

            await TryRevokeLastMessageControls();

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

                _lastAddToQueueMessage = command;
                _lastAddToQueueMessageEmbed = embed;
                _lastAddToQueueMessageTrack = track;
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

            var index = command.Data.Options.FirstOrDefault()?.Value as int?;

            if (index == null)
            {
                await command.RespondWithUserError(UserError.InvalidIndex);
                return;
            }

            var track = audioPlayer.TrackQueue.ElementAtOrDefault((int)index);

            if (track == null)
            {
                await command.RespondWithUserError(UserError.TrackNotFound);
                return;
            }

            await audioPlayer.Delete(track);

            var embed = new EmbedBuilder()
               .WithDescription($"❌ Deleting \"**{track?.Title}**\"")
               .WithColor(Color.DarkPurple)
               .Build();

            await command.RespondAsync(
                embeds: new Embed[] { embed }
            );
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
