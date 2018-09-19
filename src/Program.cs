using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using PacManBot.Services;
using PacManBot.Constants;


// Made by Samrux for fun
// GitHub repo: https://github.com/Samrux/Pac-Man-Bot

namespace PacManBot
{
    /// <summary>
    /// Program that sets up and runs the bot.
    /// </summary>
    public static class Program
    {
        public const string Version = "3.8.0";


        static async Task Main()
        {
            // Check files
            foreach (string requiredFile in new[] { Files.Config, Files.Contents })
            {
                if (!File.Exists(requiredFile))
                {
                    throw new InvalidOperationException($"Missing required file {requiredFile}: Bot can't run");
                }
            }


            // Set up configuration
            BotConfig botConfig;
            try
            {
                botConfig = JsonConvert.DeserializeObject<BotConfig>(File.ReadAllText(Files.Config));
                botConfig.LoadContent(File.ReadAllText(Files.Contents));
            }
            catch (JsonReaderException e)
            {
                throw new InvalidOperationException("The file does not contain valid JSON. Correct the mistake and try again.", e);
            }

            if (string.IsNullOrWhiteSpace(botConfig.discordToken))
            {
                throw new InvalidOperationException(
                    $"Missing {nameof(botConfig.discordToken)} in {Files.Config}: Bot can't run");
            }


            var clientConfig = new DiscordSocketConfig {
                TotalShards = botConfig.shardCount,
                LogLevel = botConfig.clientLogLevel,
                MessageCacheSize = botConfig.messageCacheSize,
                DefaultRetryMode = RetryMode.RetryRatelimit,
            };

            var client = new DiscordShardedClient(clientConfig);


            var commandConfig = new CommandServiceConfig {
                LogLevel = botConfig.commandLogLevel,
                DefaultRunMode = RunMode.Async,
                CaseSensitiveCommands = false,
            };

            var commands = new CommandService(commandConfig);

            // Set up services
            var services = new ServiceCollection()
                .AddSingleton(client)
                .AddSingleton(commands)
                .AddSingleton(botConfig)
                .AddSingleton<LoggingService>()
                .AddSingleton<StorageService>()
                .AddSingleton<GameService>()
                .AddSingleton<HelpService>()
                .AddSingleton<InputService>()
                .AddSingleton<SchedulingService>()
                .AddSingleton<ScriptingService>();

            var provider = services.BuildServiceProvider();
            foreach (var service in services)
            {
                provider.GetRequiredService(service.ServiceType);
            }

            await commands.AddModulesAsync(Assembly.GetEntryAssembly(), provider);


            // Let's go
            await new Bot(botConfig, provider).StartAsync();

            await Task.Delay(-1);
        }
    }
}
