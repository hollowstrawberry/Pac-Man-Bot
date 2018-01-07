using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;
using PacManBot.Services;


//Made by Samrux for fun
//GitHub repo: https://github.com/Samrux/Pac-Man-Bot


namespace PacManBot
{
    public class Program
    {
        private DiscordSocketClient client;
        private IConfigurationRoot customConfig;

        public static void Main(string[] args) => new Program().MainAsync().GetAwaiter().GetResult();

        public async Task MainAsync()
        {
            var builder = new ConfigurationBuilder().SetBasePath(AppContext.BaseDirectory).AddJsonFile("config.json"); //Add the configuration file
            customConfig = builder.Build(); //Build the configuration file

            var config = new DiscordSocketConfig { LogLevel = LogSeverity.Verbose, MessageCacheSize = 1000 };
            client = new DiscordSocketClient(config);

            var services = new ServiceCollection() //Build the service provider
                .AddSingleton(client)
                .AddSingleton(new CommandService(new CommandServiceConfig{ DefaultRunMode = RunMode.Async, LogLevel = LogSeverity.Verbose }))
                .AddSingleton<CommandHandler>()
                .AddSingleton<ReactionHandler>()
                .AddSingleton<LoggingService>()
                .AddSingleton<StartupService>()
                .AddSingleton(customConfig);

            var provider = services.BuildServiceProvider(); //Create the service provider

            provider.GetRequiredService<LoggingService>(); //Initialize the logging service, startup service, and command handler
            await provider.GetRequiredService<StartupService>().StartAsync();
            provider.GetRequiredService<CommandHandler>();
            provider.GetRequiredService<ReactionHandler>();

            await Task.Delay(-1); //Prevent the application from closing
        }
    }
}
