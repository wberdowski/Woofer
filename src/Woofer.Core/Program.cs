using Discord;
using Discord.Net;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Woofer.Core.Common;
using Woofer.Core.Config;

namespace Woofer.Core
{
    internal class Program
    {
        private readonly IServiceProvider _serviceProvider;
        private ConfigManager _configManager;
        private DiscordSocketClient _client;
        private ILogger _logger;
        private readonly AppModuleManager _appModuleManager;

        public Program()
        {
            _serviceProvider = CreateServices();

            _logger = _serviceProvider.GetRequiredService<ILogger<Program>>();
            _configManager = _serviceProvider.GetRequiredService<ConfigManager>();
            _appModuleManager = _serviceProvider.GetRequiredService<AppModuleManager>();

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
                Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.RealTime;
            }

            var version = AssemblyHelper.GetVersion();
            _logger.LogInformation($"Woofer v{version}");

            try
            {
                _appModuleManager.LoadModules();
                await SetupConfig();
                await SetupDiscord();
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

            if (_configManager.Config == null)
            {
                throw new Exception($"Configuration file corrupted.");
            }

            if (string.IsNullOrEmpty(_configManager.Config.BotToken))
            {
                throw new Exception($"Bot token missing. Please input your discord bot token in the config.json file.");
            }
        }

        private async Task SetupDiscord()
        {
            _client = _serviceProvider.GetRequiredService<DiscordSocketClient>();
            _client.Log += Log;
            _client.Ready += OnClientReady;
            _client.ButtonExecuted += OnButtonExecuted;
            _client.SlashCommandExecuted += OnSlashCommandExecuted;

            await _client.LoginAsync(TokenType.Bot, _configManager.Config.BotToken);
            await _client.StartAsync();
        }

        private Task Log(LogMessage msg)
        {
            var severity = msg.Severity switch
            {
                LogSeverity.Critical => LogLevel.Critical,
                LogSeverity.Error => LogLevel.Error,
                LogSeverity.Warning => LogLevel.Warning,
                LogSeverity.Info => LogLevel.Information,
                LogSeverity.Verbose => LogLevel.Trace,
                LogSeverity.Debug => LogLevel.Debug,
                _ => LogLevel.Information
            };

            _logger.Log(severity, msg.Exception, "[{Source}] {Message}", msg.Source, msg.Message);
            return Task.CompletedTask;
        }

        private async Task OnClientReady()
        {
            try
            {
                var properties = _appModuleManager.GetRegisteredCommands();
                await _client.BulkOverwriteGlobalApplicationCommandsAsync(properties.ToArray());
            }
            catch (HttpException exception)
            {
                var json = JsonConvert.SerializeObject(exception.Errors, Formatting.Indented);
                _logger.LogError(json);
            }
        }

        private async Task OnButtonExecuted(SocketMessageComponent component)
        {
            await Task.Run(() =>
            {
                _appModuleManager.RelayOnButtonExcecutedEvent(component);
            });
        }

        private async Task OnSlashCommandExecuted(SocketSlashCommand command)
        {
            _logger.LogDebug($"Command received: /{command.CommandName} {string.Join(" ", command.Data.Options.Select(o => $"{o.Name}: {o.Value}"))}");

            await Task.Run(() =>
            {
                _appModuleManager.RelayOnSlashCommandExecutedEvent(command);
            });
        }

        private async void OnApplicationExit(object? sender, EventArgs e)
        {
            if (_client != null)
            {
                await _client.StopAsync();
                await _client.LogoutAsync();

                _client.Dispose();
            }
        }
    }
}