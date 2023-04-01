using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Woofer.Core.Interfaces;
using static Woofer.Core.Common.SlashCommandDefinition;

namespace Woofer.Core.Common
{
    internal abstract class AppModule<T> : IAppModule where T : class
    {
        protected ILogger<T>? Logger { get; set; }
        private Dictionary<string, SlashCommandDefinition> _registeredModuleCommands { get; } = new();

        protected AppModule(ILogger<T>? logger)
        {
            Logger = logger;
        }

        public void InvokeHandleCommand(SocketSlashCommand command)
        {
            if (_registeredModuleCommands.TryGetValue(command.CommandName, out var cmd))
            {
                Task.Run(async () =>
                {
                    await cmd.Method.Invoke(command);
                }).ContinueWith(t =>
                {
                    if (t.IsFaulted)
                    {
                        Logger?.LogError(t.Exception?.ToString());

                        TryRespondWithInternalError(command)
                        .ContinueWith(t =>
                        {
                            if (t.IsFaulted)
                            {
                                Logger?.LogError(t.Exception?.ToString());
                            }
                        });
                    }
                });
            }
        }

        public void InvokeButtonExecuted(SocketMessageComponent component)
        {
            Task.Run(async () =>
            {
                await HandleButtonExecuted(component);
            }).ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    Logger?.LogError(t.Exception?.ToString());
                }
            });
        }

        public virtual Task HandleButtonExecuted(SocketMessageComponent component)
        {
            return Task.CompletedTask;
        }

        public virtual IEnumerable<ApplicationCommandProperties> GetRegisteredCommands()
        {
            var properties = _registeredModuleCommands
                .Select(cmd => cmd.Value.CommandProperties)
                .Cast<ApplicationCommandProperties>();

            return properties;
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

            if (!_registeredModuleCommands.TryAdd(name, new(b.Build(), handler)))
            {
                throw new ArgumentException($"Command with name \"{name}\" is already registered.");
            }
        }

        private async Task TryRespondWithInternalError(SocketSlashCommand command)
        {
            var embed = new EmbedBuilder()
                .WithAuthor($"❌ An internal error occured. Please contact bot's administrator.")
                .WithColor(Color.Red)
                .Build();

            if (!command.HasResponded)
            {
                await command.RespondAsync(embed: embed);
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
