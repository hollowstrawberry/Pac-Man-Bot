using Discord;
using Discord.Net.Providers;
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
        public static readonly string File_Config = "config.bot", File_Prefixes = "prefixes.bot", File_Scoreboard = "scoreboard.bot", File_GameMap = "map.bot", File_About = "about.bot", FileTips = "tips.bot", FileCustomMapHelp = "custommaphelp.bot";

        private DiscordSocketClient _client;
        private IConfigurationRoot _botConfig;


        public static void Main(string[] args) => new Program().MainAsync().GetAwaiter().GetResult();
        public async Task MainAsync()
        {
            var configBuilder = new ConfigurationBuilder().SetBasePath(AppContext.BaseDirectory).AddJsonFile(File_Config); //Add the configuration file
            _botConfig = configBuilder.Build(); //Build the configuration file

            //Client and its configuration
            var clientConfig = new DiscordSocketConfig { LogLevel = LogSeverity.Verbose, MessageCacheSize = 1000, WebSocketProvider = Discord.Net.Providers.WS4Net.WS4NetProvider.Instance }; //Specify websocketprovider to run properly in Windows 7
            _client = new DiscordSocketClient(clientConfig);

            //Prepare services
            var services = new ServiceCollection()
                .AddSingleton(_client)
                .AddSingleton(new CommandService(new CommandServiceConfig{ DefaultRunMode = RunMode.Async, LogLevel = LogSeverity.Verbose }))
                .AddSingleton<CommandHandler>()
                .AddSingleton<ReactionHandler>()
                .AddSingleton<LoggingService>()
                .AddSingleton<StartupService>()
                .AddSingleton(_botConfig);

            var provider = services.BuildServiceProvider();

            //Initialize services
            provider.GetRequiredService<LoggingService>();
            await provider.GetRequiredService<StartupService>().StartAsync();
            provider.GetRequiredService<CommandHandler>();
            provider.GetRequiredService<ReactionHandler>();

            //Events
            _client.Ready += async () => await UpdatePlaying(); //Updates playing message when ready or when changing guild count
            _client.JoinedGuild += async (arg) => await UpdatePlaying();
            _client.LeftGuild += async (arg) => await UpdatePlaying();


            await Task.Delay(-1); //Prevent the application from closing
        }

        public async Task UpdatePlaying()
        {
            int guilds = _client.Guilds.Count;
            await _client.SetGameAsync($"{_botConfig["prefix"]}help | {guilds} guilds");
            Console.WriteLine($"Updated guilds: {guilds}");
        }
    }
}
