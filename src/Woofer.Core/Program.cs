using Discord;
using Discord.Net;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Serilog;
using Serilog.Events;
using System.Reflection;
using Woofer.Core.Config;

namespace Woofer.Core
{
    internal class Program
    {
        private readonly IServiceProvider _serviceProvider;
        private ConfigManager _configManager;
        private DiscordSocketClient _client;
        private ILogger _logger;
        private IEnumerable<IAppModule> _modules;

        public Program()
        {
            _serviceProvider = CreateServices();
        }

        private IServiceProvider CreateServices()
        {
            var collection = new ServiceCollection()
                .AddBotServices()
                .AddBotModules();

            return collection.BuildServiceProvider();
        }

        private static void Main(string[] args)
            => new Program().RunAsync(args).GetAwaiter().GetResult();

        private async Task RunAsync(string[] args)
        {
            AppDomain.CurrentDomain.ProcessExit += OnApplicationExit;

            SetupLogging();

            var assemblyName = Assembly.GetExecutingAssembly().GetName();
            _logger.Information($"Woofer v{assemblyName.Version?.ToString(3)}");

            await SetupConfig();
            SetupModules();
            await SetupDiscord();

            await Task.Delay(Timeout.Infinite);
        }

        private void SetupLogging()
        {
            _logger = _serviceProvider.GetRequiredService<ILogger>();
        }

        private async Task SetupConfig()
        {
            _configManager = _serviceProvider.GetRequiredService<ConfigManager>();
            await _configManager.Load();

            if (_configManager.Config == null)
            {
                _logger.Error($"Configuration file corrupted.");
                return;
            }

            if (string.IsNullOrEmpty(_configManager.Config.BotToken))
            {
                _logger.Error($"Bot token missing. Please input your discord bot token in the config.json file.");
                return;
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

        private void SetupModules()
        {
            _modules = (IEnumerable<IAppModule>)_serviceProvider.GetServices(typeof(IAppModule));
            _logger.Debug($"{_modules.Count()} module(s) loaded.");
        }

        private Task Log(LogMessage msg)
        {
            var severity = msg.Severity switch
            {
                LogSeverity.Critical => LogEventLevel.Fatal,
                LogSeverity.Error => LogEventLevel.Error,
                LogSeverity.Warning => LogEventLevel.Warning,
                LogSeverity.Info => LogEventLevel.Information,
                LogSeverity.Verbose => LogEventLevel.Verbose,
                LogSeverity.Debug => LogEventLevel.Debug,
                _ => LogEventLevel.Information
            };

            _logger.Write(severity, msg.Exception, "[{Source}] {Message}", msg.Source, msg.Message);
            return Task.CompletedTask;
        }

        private async Task OnClientReady()
        {
            try
            {

                var properties = new List<ApplicationCommandProperties>();

                foreach (var module in _modules)
                {
                    var commands = module.RegisterCommands();
                    properties.AddRange(commands);
                }

                await _client.BulkOverwriteGlobalApplicationCommandsAsync(properties.ToArray());
            }
            catch (HttpException exception)
            {
                var json = JsonConvert.SerializeObject(exception.Errors, Formatting.Indented);
                _logger.Error(json);
            }
        }

        private Task OnButtonExecuted(SocketMessageComponent component)
        {
            foreach (var module in _modules)
            {
                module.HandleButtonExecuted(component);
            }

            return Task.CompletedTask;
        }

        private Task OnSlashCommandExecuted(SocketSlashCommand command)
        {
            foreach (var module in _modules)
            {
                module.HandleCommand(command);
            }

            return Task.CompletedTask;
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