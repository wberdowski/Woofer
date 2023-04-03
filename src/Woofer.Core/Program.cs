using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Woofer.Core.Common;
using Woofer.Core.Config;
using Woofer.Core.Helpers;
using Woofer.Core.Modules.AudioPlayerModule;

namespace Woofer.Core
{
    internal class Program
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ConfigManager _configManager;
        private readonly InteractionHandler _interactionHandler;
        private DiscordSocketClient? _client;
        private readonly ILogger _logger;
        private readonly AudioPlayerManager _audioPlayerManager;

        public Program()
        {
            _serviceProvider = CreateServices();

            _logger = _serviceProvider.GetRequiredService<ILogger<Program>>();
            _configManager = _serviceProvider.GetRequiredService<ConfigManager>();
            _interactionHandler = _serviceProvider.GetRequiredService<InteractionHandler>();
            _audioPlayerManager = _serviceProvider.GetRequiredService<AudioPlayerManager>();
        }

        private IServiceProvider CreateServices()
        {
            var services = new ServiceCollection()
                .AddBotServices();

            return services.BuildServiceProvider();
        }

        private static void Main(string[] args)
            => new Program().RunAsync(args).GetAwaiter().GetResult();

        private async Task RunAsync(string[] args)
        {
            AppDomain.CurrentDomain.ProcessExit += OnApplicationExit;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High;
            }

            var version = AssemblyHelper.GetVersion();
            _logger.LogInformation($"Woofer v{version}");

            try
            {
                await SetupConfig();
                await SetupDiscord();

                await _interactionHandler.InitializeAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
                await Task.Delay(1);
                return;
            }

            await Task.Delay(Timeout.Infinite);
        }

        private async Task SetupConfig()
        {
            await _configManager.Load();
            _configManager.Validate();
        }

        private async Task SetupDiscord()
        {
            _client = _serviceProvider.GetRequiredService<DiscordSocketClient>();
            _client.RegisterLogger(_logger);

            await _client.LoginAsync(TokenType.Bot, _configManager?.Config?.BotToken);
            await _client.StartAsync();
        }

        private async void OnApplicationExit(object? sender, EventArgs e)
        {
            if (_client != null)
            {
                _audioPlayerManager.Dispose();

                await _client.StopAsync();
                await _client.LogoutAsync();

                _client.Dispose();
            }
        }
    }
}