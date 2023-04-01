using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Woofer.Core.Common
{
    internal class AppModuleManager
    {
        private readonly ILogger<AppModuleManager> _logger;
        private readonly IServiceProvider _serviceProvider;

        private IEnumerable<IAppModule> _modules;

        public AppModuleManager(ILogger<AppModuleManager> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        public void LoadModules()
        {
            _modules = (IEnumerable<IAppModule>)_serviceProvider.GetServices(typeof(IAppModule));
            _logger.LogDebug($"{_modules.Count()} module(s) loaded.");
        }

        public ApplicationCommandProperties[] GetRegisteredCommands()
        {
            var properties = new List<ApplicationCommandProperties>();

            foreach (var module in _modules)
            {
                var commands = module.GetRegisteredCommands();
                properties.AddRange(commands);
            }

            return properties.ToArray();
        }

        public void InvokeButtonExcecuted(SocketMessageComponent component)
        {
            foreach (var module in _modules)
            {
                try
                {
                    module.HandleButtonExecuted(component);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, ex.Message);
                }
            }
        }

        public void InvokeOnSlashCommandExecuted(SocketSlashCommand command)
        {
            foreach (var module in _modules)
            {
                try
                {
                    module.InvokeHandleCommand(command);
                }
                catch (Exception ex)
                {
                    Task.Run(() => TryRespondWithInternalError(command))
                    .ContinueWith(t =>
                    {
                        if (t.IsFaulted)
                        {
                            _logger.LogError(t.Exception?.ToString());
                        }
                    });
                    _logger.LogError(ex.ToString());
                }
            }
        }

        private async Task TryRespondWithInternalError(SocketSlashCommand command)
        {
            var embed = new EmbedBuilder()
                .WithAuthor($"❌ An internal error occured. Please contact bot's administrator.")
                .WithColor(Color.Red)
                .Build();

            await command.ModifyOriginalResponseAsync((m) =>
            {
                m.Components = null;
                m.Embed = embed;
            });
        }
    }
}
