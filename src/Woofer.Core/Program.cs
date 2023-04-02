using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Woofer.Core.Common;
using Woofer.Core.Config;
using Woofer.Core.Extensions;
using Woofer.Core.Helpers;
using Woofer.Core.Modules.AudioPlayerModule;

namespace Woofer.Core
{
    internal class Program
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ConfigManager _configManager;
        private DiscordSocketClient? _client;
        private readonly ILogger _logger;
        private readonly InteractionService _commands;
        private readonly AudioPlayerManager _audioPlayerManager;

        public Program()
        {
            _serviceProvider = CreateServices();

            _logger = _serviceProvider.GetRequiredService<ILogger<Program>>();
            _configManager = _serviceProvider.GetRequiredService<ConfigManager>();
            _commands = _serviceProvider.GetRequiredService<InteractionService>();
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

                await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _serviceProvider);

                _commands.SlashCommandExecuted += SlashCommandExecuted;
                _commands.ComponentCommandExecuted += ComponentCommandExecuted;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
                await Task.Delay(1);
                return;
            }

            await Task.Delay(Timeout.Infinite);
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

        private async Task ComponentCommandExecuted(ComponentCommandInfo arg1, IInteractionContext arg2, IResult arg3)
        {
            if (!arg3.IsSuccess)
            {
                switch (arg3.Error)
                {
                    case InteractionCommandError.UnmetPrecondition:
                        // implement
                        break;
                    case InteractionCommandError.UnknownCommand:
                        // implement
                        break;
                    case InteractionCommandError.BadArgs:
                        // implement
                        break;
                    case InteractionCommandError.Exception:
                        // implement
                        break;
                    case InteractionCommandError.Unsuccessful:
                        // implement
                        break;
                    default:
                        break;
                }
            }

            await Task.CompletedTask;
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
                    case InteractionCommandError.UnknownCommand:
                        // implement
                        break;
                    case InteractionCommandError.BadArgs:
                        // implement
                        break;
                    case InteractionCommandError.Exception:
                        await context.Interaction.RespondWithInternalError(result.ErrorReason);
                        break;
                    case InteractionCommandError.Unsuccessful:
                        // implement
                        break;
                    default:
                        break;
                }
            }

            await Task.CompletedTask;
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
            _client.InteractionCreated += HandleInteraction;

            await _client.LoginAsync(TokenType.Bot, _configManager?.Config?.BotToken);
            await _client.StartAsync();
        }

        private async Task Log(LogMessage msg)
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

            await Task.CompletedTask;
        }

        private async Task OnClientReady()
        {
            await _commands.RegisterCommandsToGuildAsync(918118811985653783);
            await _commands.RegisterCommandsGloballyAsync(true);
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

        static bool IsDebug()
        {
#if DEBUG
            return true;
#else
                return false;
#endif
        }
    }
}