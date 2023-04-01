using Discord;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Woofer.Core.Common
{
    internal class AppModuleManager
    {
        private readonly ILogger<AppModuleManager> _logger;
        private readonly IServiceProvider _serviceProvider;

        private IEnumerable<AppModule> _modules;

        public AppModuleManager(ILogger<AppModuleManager> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        public void LoadModules()
        {
            _modules = (IEnumerable<AppModule>)_serviceProvider.GetServices(typeof(AppModule));
            _logger.LogDebug($"{_modules.Count()} module(s) loaded.");
        }

        public ApplicationCommandProperties[] GetRegisteredCommands()
        {
            var properties = new List<ApplicationCommandProperties>();

            foreach (var module in _modules)
            {
                var commands = module.RegisterCommands();
                properties.AddRange(commands);
            }

            return properties.ToArray();
        }

        public void RelayOnButtonExcecutedEvent(Discord.WebSocket.SocketMessageComponent component)
        {
            Parallel.ForEach(_modules, async module =>
            {
                try
                {
                    await module.HandleButtonExecuted(component);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, ex.Message);
                }
            });
        }

        public void RelayOnSlashCommandExecutedEvent(Discord.WebSocket.SocketSlashCommand command)
        {
            Parallel.ForEach(_modules, async module =>
            {
                try
                {
                    await module.HandleCommand(command);
                }
                catch (Exception ex)
                {
                    var embed = new EmbedBuilder()
                        .WithAuthor($"❌ An internal error occured. Please contact bot's administrator.")
                        .WithColor(Color.Red)
                        .Build();

                    try
                    {
                        await command.ModifyOriginalResponseAsync((m) =>
                        {
                            m.Components = null;
                            m.Embed = embed;
                        });
                    }
                    catch (Exception e)
                    {

                    }

                    _logger.LogError(ex, ex.Message);
                }
            });
        }
    }
}
