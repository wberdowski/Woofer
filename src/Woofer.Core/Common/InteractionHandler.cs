using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using System.Reflection;
using Woofer.Core.Extensions;

namespace Woofer.Core.Common
{
    public class InteractionHandler
    {
        private readonly DiscordSocketClient _client;
        private readonly ILogger<InteractionHandler> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly InteractionService _commands;

        public InteractionHandler(
            DiscordSocketClient client,
            ILogger<InteractionHandler> logger,
            IServiceProvider serviceProvider,
            InteractionService commands)
        {
            _client = client;
            _logger = logger;
            _serviceProvider = serviceProvider;
            _commands = commands;
        }

        public async Task InitializeAsync()
        {
            await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _serviceProvider);
            _commands.SlashCommandExecuted += SlashCommandExecuted;
            _client.InteractionCreated += HandleInteraction;
            _client.Ready += OnClientReady;
        }

        private async Task OnClientReady()
        {
            await _commands.RegisterCommandsGloballyAsync(true);
        }

        private async Task HandleInteraction(SocketInteraction arg)
        {
            try
            {
                var ctx = new SocketInteractionContext(_client, arg);
                await _commands.ExecuteCommandAsync(ctx, _serviceProvider);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());

                if (arg.Type == InteractionType.ApplicationCommand)
                {
                    await arg.GetOriginalResponseAsync().ContinueWith(async (msg) => await msg.Result.DeleteAsync());
                }
            }
        }

        private async Task SlashCommandExecuted(SlashCommandInfo command, IInteractionContext context, IResult result)
        {
            if (!result.IsSuccess)
            {
                switch (result.Error)
                {
                    case InteractionCommandError.UnmetPrecondition:
                        await context.Interaction.RespondWithUserError(result.ErrorReason);
                        break;
                    case InteractionCommandError.Exception:
                        await context.Interaction.RespondWithInternalError(result.ErrorReason);
                        break;
                }
            }

            await Task.CompletedTask;
        }
    }
}
