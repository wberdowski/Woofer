using Discord;
using Discord.WebSocket;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Woofer.Core.Helpers;
using Woofer.Core.Modules.Common.Enums;

namespace Woofer.Core.Modules.Common.Extensions
{
    internal static class ModuleMessageExtensions
    {
        public static async Task RespondWithUserError(this SocketSlashCommand command, UserError error, bool ephemeral = true)
        {
            var embedBuilder = new EmbedBuilder()
                .WithAuthor($"⛔ {Regex.Replace(error.ToString(), "([a-z])([A-Z])", "$1 $2")}")
                .WithColor(Color.Red);

            await command.RespondAsync(embed: embedBuilder.Build(), ephemeral: ephemeral);
        }

        public static async Task RespondWithInternalError(this SocketSlashCommand command, Exception ex)
        {
            var details = $"Version: {AssemblyHelper.GetVersion()}\n" +
                $"Date: {DateTime.UtcNow}\n" +
                $"OS: {Environment.OSVersion}\n" +
                $"Memory: {Process.GetCurrentProcess().PrivateMemorySize64 / 1024 / 1024}MB\n" +
                $"\n" +
                $"{ex}";

            var embed = new EmbedBuilder()
                .WithAuthor($"❌ An internal error occured while executing /{command.CommandName} command.")
                .WithDescription(
                    $"If this error persists, please report it with all the necessary details at https://github.com/wberdowski/Woofer/issues\n" +
                    $"```fix\n" +
                    $"Checksum: {(uint)details.GetHashCode()}\n" +
                    $"{details}```"
                )
                .WithColor(Color.Red)
                .Build();

            if (!command.HasResponded)
            {
                await command.RespondAsync(embed: embed, ephemeral: true);
                return;
            }

            await command.ModifyOriginalResponseAsync((m) =>
            {
                m.Components = null;
                m.Embed = embed;
            });
        }
    }
}
