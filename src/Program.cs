using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Discord;
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
            PmConfig config;
            try
            {
                config = JsonConvert.DeserializeObject<PmConfig>(File.ReadAllText(Files.Config));
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
            var serviceCollection = new ServiceCollection()
                .AddSingleton(config)
                .AddSingleton<PmBot>()
                .AddSingleton<PmDiscordClient>()
                .AddSingleton<LoggingService>()
                .AddSingleton<StorageService>()
                .AddSingleton<PmCommandService>()
                .AddSingleton<InputService>()
                .AddSingleton<GameService>()
                .AddSingleton<PmCommandService>()
                .AddSingleton<SchedulingService>()
                .AddSingleton<ScriptingService>();

            var services = serviceCollection.BuildServiceProvider();
            await services.Get<LoggingService>().Log(LogSeverity.Info, $"Pac-Man Bot v{Version}");


            // Let's go
            await services.Get<PmBot>().StartAsync();

            await Task.Delay(-1);
        }
    }
}
