using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using PacManBot.Services;
using PacManBot.Constants;
using Newtonsoft.Json;


//Made by Samrux for fun
//GitHub repo: https://github.com/Samrux/Pac-Man-Bot


namespace PacManBot
{
    public class BotConfig
    {
        public string defaultPrefix;
        public string discordToken;
        public string[] httpToken;
        public int shardCount;
        public int messageCacheSize;
        public LogSeverity clientLogLevel;
        public LogSeverity commandLogLevel;
    }


    public class Bot
    {
        private DiscordShardedClient client;
        private LoggingService logger;
        private StorageService storage;
        private BotConfig botConfig;

        private CancellationTokenSource cancelReconnectTimeout = null;
        private Stopwatch guildCountTimer = null;
        int shardsReady = 0;


        public static void Main(string[] args) => new Bot().MainAsync().GetAwaiter().GetResult();


        public async Task MainAsync()
        {
            if (!File.Exists(BotFile.Config)) throw new Exception($"Configuration file {BotFile.Config} is missing.");

            botConfig = JsonConvert.DeserializeObject<BotConfig>(File.ReadAllText(BotFile.Config));
            var clientConfig = new DiscordSocketConfig { TotalShards = botConfig.shardCount, LogLevel = botConfig.clientLogLevel, MessageCacheSize = botConfig.messageCacheSize};
            var commandConfig = new CommandServiceConfig { DefaultRunMode = RunMode.Async, LogLevel = botConfig.commandLogLevel };

            client = new DiscordShardedClient(clientConfig);

            //Prepare services
            var services = new ServiceCollection()
                .AddSingleton(client)
                .AddSingleton(new CommandService(commandConfig))
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
            client.ShardReady += OnShardReady;
            client.ShardConnected += OnShardConnected;
            client.ShardDisconnected += OnShardDisconnected;
            client.JoinedGuild += OnJoinedGuild;
            client.LeftGuild += OnLeftGuild;
            client.ChannelDestroyed += OnChannelDestroyed;

            await Task.Delay(-1); //Prevent the application from closing
        }



        // Events

        private Task OnShardConnected(DiscordSocketClient shard)
        {
            if (cancelReconnectTimeout != null)
            {
                cancelReconnectTimeout.Cancel();
                logger.Log(LogSeverity.Info, "Client reconnected. Timeout cancelled.");
                cancelReconnectTimeout = new CancellationTokenSource();
            }

            return Task.CompletedTask;
        }


        private Task OnShardDisconnected(Exception e, DiscordSocketClient shard)
        {
            if (cancelReconnectTimeout == null) cancelReconnectTimeout = new CancellationTokenSource();

            logger.Log(LogSeverity.Info, "Client disconnected. Starting reconnection timeout...");

            Task.Delay(TimeSpan.FromSeconds(100), cancelReconnectTimeout.Token).ContinueWith(_ =>
            {
                if (shard.ConnectionState != ConnectionState.Connected)
                {
                    logger.Log(LogSeverity.Critical, "Reconnection timeout expired. Shutting down...");
                    Environment.Exit(1);
                }
            });

            return Task.CompletedTask;
        }


        private Task OnShardReady(DiscordSocketClient shard)
        {
            if (++shardsReady == client.Shards.Count)
            {
                logger.Log(LogSeverity.Info, "All shards ready");
                _ = UpdateGuildCountAsync(); // Discarding allows the async code to run without blocking the gateway task
            }
            return Task.CompletedTask;
        }


        private Task OnJoinedGuild(SocketGuild guild)
        {
            _ = UpdateGuildCountAsync();
            return Task.CompletedTask;
        }


        private Task OnLeftGuild(SocketGuild guild)
        {
            for (int i = 0; i < storage.GameInstances.Count; i++) //Removes leftover games in the guild we left
            {
                if (storage.GameInstances[i].Guild?.Id == guild.Id) storage.DeleteGame(i);
            }

            _ = UpdateGuildCountAsync();
            return Task.CompletedTask;
        }


        private Task OnChannelDestroyed(SocketChannel channel)
        {
            for (int i = 0; i < storage.GameInstances.Count; i++) //Removes a leftover game in that channel
            {
                if (storage.GameInstances[i].channelId == channel.Id) storage.DeleteGame(i);
            }

            return Task.CompletedTask;
        }




        private async Task UpdateGuildCountAsync() //I have to do this so that exceptions don't go silent
        {
            try
            {
                await UpdateGuildCountInternal();
            }
            catch (Exception e)
            {
                await logger.Log(LogSeverity.Error, $"{e}");
            }
        }


        private async Task UpdateGuildCountInternal()
        {
            if (guildCountTimer == null || guildCountTimer.Elapsed.TotalMinutes >= 20)
            {
                int guilds = client.Guilds.Count;

                await client.SetGameAsync($"{botConfig.defaultPrefix}help | {guilds} guilds");

                string[] website = { $"bots.discord.pw",
                                     $"discordbots.org" };

                for (int i = 0; i < website.Length && i < botConfig.httpToken.Length; i++)
                {
                    if (string.IsNullOrWhiteSpace(botConfig.httpToken[i])) continue;

                    string requesturi = "https://" + website[i] + $"/api/bots/{client.CurrentUser.Id}/stats";
                    var webclient = new HttpClient();
                    var content = new StringContent($"{{\"server_count\": {guilds}}}", System.Text.Encoding.UTF8, "application/json");
                    webclient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(botConfig.httpToken[i]);
                    var response = await webclient.PostAsync(requesturi, content);

                    await logger.Log(LogSeverity.Verbose, $"Sent guild count to {website[i]} - {(response.IsSuccessStatusCode ? "Success" : $"Response:\n{response}")}");
                }

                guildCountTimer = Stopwatch.StartNew();

                await logger.Log(LogSeverity.Info, $"Guild count updated to {guilds}");
            }
        }
    }
}
