using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Discord;
using Discord.WebSocket;
using PacManBot.Utils;
using PacManBot.Services;
using PacManBot.Constants;
using PacManBot.Extensions;


// Made by Samrux for fun
// GitHub repo: https://github.com/Samrux/Pac-Man-Bot

namespace PacManBot
{
    /// <summary>
    /// Program that sets up and starts the bot.
    /// </summary>
    public static class Program
    {
        /// <summary>The bot program's displayed version.</summary>
        public static readonly string Version = Assembly.GetEntryAssembly().GetName().Version.ToString().TrimEnd(".0");

        /// <summary>The random number generator used throughout the program.</summary>
        public static readonly ConcurrentRandom Random = new ConcurrentRandom();


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
            BotConfig config;
            try
            {
                config = JsonConvert.DeserializeObject<BotConfig>(File.ReadAllText(Files.Config));
                config.LoadContent(File.ReadAllText(Files.Contents));
            }
            catch (JsonReaderException e)
            {
                throw new InvalidOperationException("The file does not contain valid JSON. Correct the mistake and try again.", e);
            }

            if (string.IsNullOrWhiteSpace(config.discordToken))
            {
                throw new InvalidOperationException(
                    $"Missing {nameof(config.discordToken)} in {Files.Config}: Bot can't run");
            }


            var clientConfig = new DiscordSocketConfig {
                TotalShards = config.shardCount,
                LogLevel = config.clientLogLevel,
                MessageCacheSize = config.messageCacheSize,
                ConnectionTimeout = config.connectionTimeout,
                DefaultRetryMode = RetryMode.RetryRatelimit,
            };

            var client = new DiscordShardedClient(clientConfig);

            // Set up services
            var serviceCollection = new ServiceCollection()
                .AddSingleton<Bot>()
                .AddSingleton(config)
                .AddSingleton(client)
                .AddSingleton<LoggingService>()
                .AddSingleton<StorageService>()
                .AddSingleton<PmCommandService>()
                .AddSingleton<InputService>()
                .AddSingleton<GameService>()
                .AddSingleton<PmCommandService>()
                .AddSingleton<SchedulingService>()
                .AddSingleton<ScriptingService>();

            var services = serviceCollection.BuildServiceProvider();
            var logger = services.Get<LoggingService>();

            await logger.Log(LogSeverity.Info, "Booting up...");

            await services.Get<PmCommandService>().AddModulesAsync();
            services.Get<GameService>().LoadGames();


            // Let's go
            await services.Get<Bot>().StartAsync();

            await Task.Delay(-1);
        }
    }
}
