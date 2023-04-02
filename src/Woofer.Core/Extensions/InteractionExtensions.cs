using Discord;
using System.Text.RegularExpressions;
using Woofer.Core.Enums;

namespace Woofer.Core.Extensions
{
    internal static class InteractionExtensions
    {
        public static async Task RespondWithUserError(this IDiscordInteraction interaction, UserError error, bool ephemeral = true)
        {
            var embedBuilder = new EmbedBuilder()
                .WithAuthor($"❌ {Regex.Replace(error.ToString(), "([a-z])([A-Z])", "$1 $2")}")
                .WithColor(Color.Red);

            await interaction.RespondAsync(embed: embedBuilder.Build(), ephemeral: ephemeral);
        }

        public static async Task RespondWithUserError(this IDiscordInteraction interaction, string error, bool ephemeral = true)
        {
            var embedBuilder = new EmbedBuilder()
                .WithAuthor($"❌ {error}")
                .WithColor(Color.Red);

            await interaction.RespondAsync(embed: embedBuilder.Build(), ephemeral: ephemeral);
        }

        public static async Task RespondWithInternalError(this IDiscordInteraction interaction, string message)
        {
            var embed = new EmbedBuilder()
                .WithAuthor($"❌ An internal error occured while executing the command.")
                .WithDescription(
                    $"Error message: **{message}**\n\n" +
                    $"If this error persists, please report it at:\nhttps://github.com/wberdowski/Woofer/issues"
                )
                .WithColor(Color.Red)
                .Build();

            if (!interaction.HasResponded)
            {
                await interaction.RespondAsync(embed: embed, ephemeral: true);
                return;
            }

            await interaction.ModifyOriginalResponseAsync((m) =>
            {
                m.Components = null;
                m.Embed = embed;
            });
        }
    }
}
