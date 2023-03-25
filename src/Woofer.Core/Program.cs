using Discord;
using Discord.Net;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Diagnostics;
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
            var services = new ServiceCollection()
                .AddBotServices();

            return services.BuildServiceProvider();
        }

        private static void Main(string[] args)
            => new Program().RunAsync(args).GetAwaiter().GetResult();

        private async Task RunAsync(string[] args)
        {
            AppDomain.CurrentDomain.ProcessExit += OnApplicationExit;
            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.RealTime;

            SetupLogging();

            var assemblyName = Assembly.GetExecutingAssembly().GetName();
            _logger.LogInformation($"Woofer v{assemblyName.Version?.ToString(3)}");

            try
            {
                await SetupConfig();
                SetupModules();
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

        private void SetupLogging()
        {
            _logger = _serviceProvider.GetRequiredService<ILogger<Program>>();
        }

        private async Task SetupConfig()
        {
            _configManager = _serviceProvider.GetRequiredService<ConfigManager>();
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

        private void SetupModules()
        {
            _modules = (IEnumerable<IAppModule>)_serviceProvider.GetServices(typeof(IAppModule));
            _logger.LogDebug($"{_modules.Count()} module(s) loaded.");
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
                _logger.LogError(json);
            }
        }

        private async Task OnButtonExecuted(SocketMessageComponent component)
        {
            await Task.Run(() =>
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
            });
        }

        private async Task OnSlashCommandExecuted(SocketSlashCommand command)
        {
            await Task.Run(() =>
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

                        await command.ModifyOriginalResponseAsync((m) =>
                        {
                            m.Components = null;
                            m.Embed = embed;
                        });

                        _logger.LogError(ex, ex.Message);
                    }
                });
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