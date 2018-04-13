using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PacManBot.Services;
using PacManBot.Constants;


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
            client.Ready += async () => await UpdateGuildCount(); //Updates playing message when ready or when changing guild count
            client.JoinedGuild += OnJoinedGuild;
            client.LeftGuild += OnLeftGuild;


            await Task.Delay(-1); //Prevent the application from closing
        }


        private async Task OnJoinedGuild(SocketGuild guild)
        {
            await UpdateGuildCount();
        }


        private async Task OnLeftGuild(SocketGuild guild)
        {
            await UpdateGuildCount();

            for (int i = 0; i < storage.gameInstances.Count; i++)
            {
                var guildChannel = client.GetChannel(storage.gameInstances[i].channelId) as SocketGuildChannel;
                if (guildChannel != null && guildChannel.Guild.Id == guild.Id)
                {
                    await logger.Log(LogSeverity.Verbose, $"Removing game at {storage.gameInstances[i].channelId}");
                    if (File.Exists(storage.gameInstances[i].GameFile)) File.Delete(storage.gameInstances[i].GameFile);
                    storage.gameInstances.RemoveAt(i);
                }
            }
        }


        private async Task UpdateGuildCount()
        {
            int guilds = client.Guilds.Count;
            await client.SetGameAsync($"{botConfig["prefix"]}help | {guilds} guilds");
            await logger.Log(LogSeverity.Info, $"Guild count is now {guilds}");

            if (!string.IsNullOrWhiteSpace(botConfig["httptoken"]))
            {
                await UpdateServerGuildCount(guilds);
            }
        }


        private Task UpdateServerGuildCount(int count)
        {
            var request = (HttpWebRequest)WebRequest.Create($"https://bots.discord.pw/api/bots/{client.CurrentUser.Id}/stats");
            request.ContentType = "application/json";
            request.Method = "POST";
            request.Headers.Add("Authorization", botConfig["httptoken"]);

            using (var writer = new StreamWriter(request.GetRequestStream()))
            {
                writer.Write($"{{\n\"server_count\": {count}\n}}");
            }

            string response;
            using (var reader = new StreamReader(((HttpWebResponse)request.GetResponse()).GetResponseStream()))
            {
                response = reader.ReadToEnd();
            }

            return logger.Log(LogSeverity.Verbose, $"Sent server count to server. {(string.IsNullOrWhiteSpace(response) ? "Successful." : $"Response:\n{response}")}");
        }
    }
}
