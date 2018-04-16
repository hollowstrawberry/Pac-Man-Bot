using System;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PacManBot.Services;
using PacManBot.Constants;
using System.Net.Http;
using System.Net.Http.Headers;


//Made by Samrux for fun
//GitHub repo: https://github.com/Samrux/Pac-Man-Bot


namespace PacManBot
{
    public class Program
    {
        private DiscordSocketClient client;
        private LoggingService logger;
        private StorageService storage;
        private IConfigurationRoot botConfig;

        private Stopwatch serverguildcounttimer = null;


        public static void Main(string[] args) => new Program().MainAsync().GetAwaiter().GetResult();

        public async Task MainAsync()
        {
            var configBuilder = new ConfigurationBuilder().SetBasePath(AppContext.BaseDirectory).AddJsonFile(BotFile.Config); //Add the configuration file
            botConfig = configBuilder.Build(); //Build the configuration file

            //Client and its configuration
            if (!Int32.TryParse(botConfig["messagecachesize"], out int cacheSize)) cacheSize = 100;
            var clientConfig = new DiscordSocketConfig { LogLevel = LogSeverity.Verbose, MessageCacheSize = cacheSize, /*WebSocketProvider = Discord.Net.Providers.WS4Net.WS4NetProvider.Instance*/ }; //Specify websocketprovider to run properly in Windows 7
            client = new DiscordSocketClient(clientConfig);

            //Prepare services
            var services = new ServiceCollection()
                .AddSingleton(client)
                .AddSingleton(new CommandService(new CommandServiceConfig{ DefaultRunMode = RunMode.Async, LogLevel = LogSeverity.Verbose }))
                .AddSingleton<CommandHandler>()
                .AddSingleton<ReactionHandler>()
                .AddSingleton<ScriptingService>()
                .AddSingleton<LoggingService>()
                .AddSingleton<StorageService>()
                .AddSingleton<StartupService>()
                .AddSingleton(botConfig);

            var provider = services.BuildServiceProvider();

            //Initialize services
            logger = provider.GetRequiredService<LoggingService>();
            storage = provider.GetRequiredService<StorageService>();
            await provider.GetRequiredService<StartupService>().StartAsync();
            provider.GetRequiredService<CommandHandler>();
            provider.GetRequiredService<ReactionHandler>();
            provider.GetRequiredService<ScriptingService>();

            //Events
            client.Ready += OnReady;
            client.JoinedGuild += OnJoinedGuild;
            client.LeftGuild += OnLeftGuild;
            client.ChannelDestroyed += OnChannelDestroyed;


            await Task.Delay(-1); //Prevent the application from closing
        }


        //Events

        private async Task OnReady()
        {
            await UpdateGuildCount();
        }


        private async Task OnJoinedGuild(SocketGuild guild)
        {
            await UpdateGuildCount();
        }


        private async Task OnLeftGuild(SocketGuild guild)
        {
            await UpdateGuildCount();

            for (int i = 0; i < storage.gameInstances.Count; i++) //Removes leftover games in the guild we left
            {
                if (storage.gameInstances[i].Guild?.Id == guild.Id) storage.DeleteGame(i);
            }
        }


        private Task OnChannelDestroyed(SocketChannel channel)
        {
            for (int i = 0; i < storage.gameInstances.Count; i++) //Removes a leftover game in that channel
            {
                if (storage.gameInstances[i].channelId == channel.Id) storage.DeleteGame(i);
            }
            return Task.CompletedTask;
        }



        private async Task UpdateGuildCount()
        {
            int guilds = client.Guilds.Count;

            await client.SetGameAsync($"{botConfig["prefix"]}help | {guilds} guilds");
            await logger.Log(LogSeverity.Info, $"Guild count is now {guilds}");

            // Update online guild count
            if (serverguildcounttimer == null || serverguildcounttimer.Elapsed.TotalMinutes >= 15.0)
            {
                string[] website = { $"bots.discord.pw",
                                     $"discordbots.org" };

                for (int i = 0; i < 2; i++)
                {
                    if (string.IsNullOrWhiteSpace(botConfig[$"httptoken{i}"])) continue;

                    string requesturi = "https://" + website[i] + $"/api/bots/{client.CurrentUser.Id}/stats";
                    var webclient = new HttpClient();
                    var content = new StringContent($"{{\"server_count\": {guilds}}}", System.Text.Encoding.UTF8, "application/json");
                    webclient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(botConfig[$"httptoken{i}"]);
                    var response = await webclient.PostAsync(requesturi, content);

                    await logger.Log(LogSeverity.Verbose, $"Sent guild count to {website[i]}. {(response.IsSuccessStatusCode ? "Success" : $"Response:\n{response}")}");
                }

                serverguildcounttimer = Stopwatch.StartNew();
            }
        }
    }
}
