using Discord;
using Discord.WebSocket;
using static Woofer.Core.Common.SlashCommandDefinition;

namespace Woofer.Core.Common
{
    internal abstract class AppModule
    {
        protected Dictionary<string, SlashCommandDefinition> RegisteredModuleCommands { get; } = new();

        public virtual async Task HandleCommand(SocketSlashCommand command)
        {
            if (RegisteredModuleCommands.TryGetValue(command.CommandName, out var cmd))
            {
                await cmd.Method.Invoke(command);
            }
        }

        public virtual IEnumerable<ApplicationCommandProperties> RegisterCommands()
        {
            var properties = RegisteredModuleCommands
                .Select(cmd => cmd.Value.CommandProperties)
                .Cast<ApplicationCommandProperties>();

            return properties;
        }

        public virtual async Task HandleButtonExecuted(SocketMessageComponent component)
        {
            await Task.CompletedTask;
        }

        protected void RegisterCommand(string name, string description, SlashCommandHandler handler, SlashCommandBuilder builder, params string[] aliases)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            if (aliases.Length != aliases.Distinct().Count())
            {
                throw new ArgumentException("Command names cannot repeat.");
            }

            AddCommand(name, description, handler, builder);

            foreach (var alias in aliases)
            {
                AddCommand(alias, description, handler, builder);
            }
        }

        protected void RegisterCommand(string name, string description, SlashCommandHandler handler, params string[] aliases)
        {
            RegisterCommand(name, description, handler, new(), aliases);
        }

        private void AddCommand(string name, string description, SlashCommandHandler handler, SlashCommandBuilder builder)
        {
            var b = builder
                .WithName(name)
                .WithDescription(description);

            if (!RegisteredModuleCommands.TryAdd(name, new(b.Build(), handler)))
            {
                throw new ArgumentException($"Command with name \"{name}\" is already registered.");
            }
        }
    }
}
