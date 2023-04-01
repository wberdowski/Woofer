using Discord;
using Discord.WebSocket;
using System.Text.RegularExpressions;
using Woofer.Core.Modules.Common.Enums;

namespace Woofer.Core.Modules.Common.Extensions
{
    internal static class ModuleMessageExtensions
    {
        public static async Task RespondWithUserError(this SocketSlashCommand command, UserError error, bool ephemeral = true)
        {
            var embedBuilder = new EmbedBuilder()
                .WithAuthor($"❌ Error")
                .WithDescription($"{Regex.Replace(error.ToString(), "([a-z])([A-Z])", "$1 $2")}")
                .WithColor(Color.Red);

            await command.RespondAsync(embed: embedBuilder.Build(), ephemeral: ephemeral);
        }
    }
}
