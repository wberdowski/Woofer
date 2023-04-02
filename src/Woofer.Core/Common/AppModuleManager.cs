using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using Woofer.Core.Interfaces;

namespace Woofer.Core.Common
{
    internal class AppModuleManager
    {
        public ReadOnlyCollection<SlashCommandProperties>? RegisteredCommands { get; private set; }

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

        public void RegisterCommands()
        {
            var cmds = new List<SlashCommandProperties>();

            foreach (var module in _modules)
            {
                var commands = module.RegisterCommands();
                cmds.AddRange(commands);
            }

            RegisteredCommands = cmds.AsReadOnly();
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
                module.InvokeHandleCommand(command);
            }
        }
    }
}
