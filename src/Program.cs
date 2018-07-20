using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Discord.Commands;
using Discord.WebSocket;
using PacManBot.Services;
using PacManBot.Constants;


// Made by Samrux for fun
// GitHub repo: https://github.com/Samrux/Pac-Man-Bot

namespace PacManBot
{
    /// <summary>
    /// Sets up configurations and services before starting the bot.
    /// </summary>
    public static class Program
    {
        static async Task Main()
        {
            // Check files
            foreach (string requiredFile in new[] { Files.Config, Files.Contents })
            {
                if (!File.Exists(requiredFile)) throw new Exception($"Missing required file {requiredFile}: Bot can't run");
            }
            

            // Set up configurations
            var botConfig = JsonConvert.DeserializeObject<BotConfig>(File.ReadAllText(Files.Config));
            if (string.IsNullOrWhiteSpace(botConfig.discordToken))
            {
                throw new Exception($"Missing {nameof(botConfig.discordToken)} in {Files.Config}: Bot can't run");
            }

            var clientConfig = new DiscordSocketConfig
            {
                TotalShards = botConfig.shardCount,
                LogLevel = botConfig.clientLogLevel,
                MessageCacheSize = botConfig.messageCacheSize
            };
            var client = new DiscordShardedClient(clientConfig);

            var commandConfig = new CommandServiceConfig
            {
                DefaultRunMode = RunMode.Async,
                LogLevel = botConfig.commandLogLevel,
                ThrowOnError = true
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
                .AddSingleton<InputService>()
                .AddSingleton<SchedulingService>()
                .AddSingleton<ScriptingService>();

            var provider = services.BuildServiceProvider();
            foreach (var service in services)
            {
                provider.GetRequiredService(service.ServiceType); // Create instance
            }


            await commands.AddModulesAsync(Assembly.GetEntryAssembly(), provider);


            // Let's go
            await new Bot(botConfig, provider).StartAsync();

            await Task.Delay(-1);
        }
    }
}
