using Discord;
using Discord.WebSocket;
using System.Text.RegularExpressions;
using Woofer.Core.Common.Enums;

namespace Woofer.Core.Modules.Extensions
{
    internal static class ModuleMessageExtensions
    {
        public static async Task RespondWithUserError(this SocketSlashCommand command, UserError error)
        {
            var embedBuilder = new EmbedBuilder()
                .WithAuthor($"❌ Error")
                .WithDescription($"{Regex.Replace(error.ToString(), "([a-z])([A-Z])", "$1 $2")}")
                .WithColor(Color.Red);

            await command.RespondAsync(embed: embedBuilder.Build());
        }
    }
}
