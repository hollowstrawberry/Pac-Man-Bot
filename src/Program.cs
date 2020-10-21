using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using DSharpPlus;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using PacManBot.Constants;
using PacManBot.Extensions;
using PacManBot.Services;
using PacManBot.Utils;


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
            PmBotConfig config;
            try
            {
                config = JsonConvert.DeserializeObject<PmBotConfig>(File.ReadAllText(Files.Config));
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

            // Set up services
            var log = new LoggingService(config);
            var discord = new DiscordShardedClient(config.MakeClientConfig(log));

            var serviceCollection = new ServiceCollection()
                .AddSingleton(config)
                .AddSingleton(log)
                .AddSingleton(discord)
                .AddSingleton<PmBot>()
                .AddSingleton<DatabaseService>()
                .AddSingleton<InputService>()
                .AddSingleton<GameService>()
                .AddSingleton<SchedulingService>()
                .AddSingleton<ScriptingService>()
                .AddSingleton<WordService>();

            var services = serviceCollection.BuildServiceProvider();

            // Let's go
            try
            {
                log.Info($"Pac-Man Bot v{Version}");
                await services.Get<PmBot>().StartAsync();
            }
            catch (Exception e)
            {
                log.Fatal($"While starting the bot: {e}");
                await Task.Delay(5000);
            }

            await Task.Delay(-1);
        }
    }
}
