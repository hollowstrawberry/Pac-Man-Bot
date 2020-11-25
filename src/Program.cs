using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using DSharpPlus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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


        public static Task Main(string[] args)
        {
            // Load configuration
            foreach (string requiredFile in new[] { Files.Config, Files.Contents })
                if (!File.Exists(requiredFile))
                    throw new InvalidOperationException($"Missing required file {requiredFile}: Bot can't run");

            PmBotConfig config;
            try
            {
                config = JsonConvert.DeserializeObject<PmBotConfig>(File.ReadAllText(Files.Config));
                config.LoadContent(File.ReadAllText(Files.Contents));
            }
            catch (JsonReaderException e)
            {
                throw new InvalidOperationException("A required file contains invalid JSON. Correct the mistake and try again.", e);
            }

            if (string.IsNullOrWhiteSpace(config.discordToken))
                throw new InvalidOperationException($"Missing {nameof(config.discordToken)} in {Files.Config}: Bot can't run");


            // Set up and run the bot
            var logger = new LoggingService(config);

            return Host.CreateDefaultBuilder(args)
                .ConfigureLogging(lb => lb.ClearProviders().AddProvider(logger))
                .ConfigureServices(services => services
                    .AddHostedService<PmBot>()
                    .AddSingleton(config)
                    .AddSingleton(logger)
                    .AddSingleton(_ => config.MakeClientConfig(logger))
                    .AddSingleton<DiscordShardedClient>()
                    .AddSingleton<DatabaseService>()
                    .AddSingleton<InputService>()
                    .AddSingleton<GameService>()
                    .AddSingleton<SchedulingService>())
                .RunConsoleAsync();
        }
    }
}
