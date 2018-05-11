using System;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Headers;
using Discord;
using Discord.WebSocket;
using PacManBot.Services;

namespace PacManBot
{
    public class Bot
    {
        private BotConfig botConfig;
        private IServiceProvider services;

        private DiscordShardedClient client;
        private LoggingService logger;
        private StorageService storage;

        int shardsReady = 0;
        bool reconnecting = false;
        private Stopwatch guildCountTimer = null;
        private CancellationTokenSource cancelReconnectTimeout = null;


        public Bot(BotConfig botConfig, IServiceProvider services)
        {
            this.botConfig = botConfig;
            this.services = services;

            client = services.Get<DiscordShardedClient>();
            logger = services.Get<LoggingService>();
            storage = services.Get<StorageService>();

            //Events
            client.ShardReady += OnShardReady;
            client.ShardConnected += OnShardConnected;
            client.ShardDisconnected += OnShardDisconnected;
            client.JoinedGuild += OnJoinedGuild;
            client.LeftGuild += OnLeftGuild;
            client.ChannelDestroyed += OnChannelDestroyed;
        }


        public async Task StartAsync()
        {
            await client.LoginAsync(TokenType.Bot, botConfig.discordToken);
            await client.StartAsync();
        }




        private Task OnShardConnected(DiscordSocketClient currentShard)
        {
            bool allConnected = true;
            foreach (var shard in client.Shards) if (shard.ConnectionState != ConnectionState.Connected) allConnected = false;

            if (allConnected && cancelReconnectTimeout != null)
            {
                reconnecting = false;
                cancelReconnectTimeout.Cancel();
                logger.Log(LogSeverity.Info, "All clients reconnected. Timeout cancelled.");
                cancelReconnectTimeout = new CancellationTokenSource();
            }

            return Task.CompletedTask;
        }


        private Task OnShardDisconnected(Exception e, DiscordSocketClient currentShard)
        {
            if (reconnecting) return Task.CompletedTask;
            reconnecting = true;

            if (cancelReconnectTimeout == null) cancelReconnectTimeout = new CancellationTokenSource();

            logger.Log(LogSeverity.Info, "Client disconnected. Starting reconnection timeout...");

            Task.Delay(TimeSpan.FromSeconds(180), cancelReconnectTimeout.Token).ContinueWith(_ =>
            {
                foreach (var shard in client.Shards) if (shard.ConnectionState != ConnectionState.Connected)
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
            foreach (var game in storage.GameInstances.Where(game => game.Guild?.Id == guild.Id).ToArray())
            {
                storage.DeleteGame(game);
            }

            _ = UpdateGuildCountAsync();
            return Task.CompletedTask;
        }


        private Task OnChannelDestroyed(SocketChannel channel)
        {
            foreach (var game in storage.GameInstances.Where(game => game.channelId == channel.Id).ToArray())
            {
                storage.DeleteGame(game);
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

                using (var httpClient = new HttpClient())
                {
                    string[] website = { $"bots.discord.pw", $"discordbots.org" };
                    for (int i = 0; i < website.Length && i < botConfig.httpToken.Length; i++)
                    {
                        if (string.IsNullOrWhiteSpace(botConfig.httpToken[i])) continue;

                        string requesturi = "https://" + website[i] + $"/api/bots/{client.CurrentUser.Id}/stats";
                        var content = new StringContent($"{{\"server_count\": {guilds}}}", System.Text.Encoding.UTF8, "application/json");
                        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(botConfig.httpToken[i]);
                        var response = await httpClient.PostAsync(requesturi, content);

                        await logger.Log(response.IsSuccessStatusCode ? LogSeverity.Verbose : LogSeverity.Warning,
                                         $"Sent guild count to {website[i]} - {(response.IsSuccessStatusCode ? "Success" : $"Response:\n{response}")}");
                    }
                }

                guildCountTimer = Stopwatch.StartNew();

                await logger.Log(LogSeverity.Info, $"Guild count updated to {guilds}");
            }
        }
    }


    public class BotConfig
    {
        public string defaultPrefix = "<";
        public string discordToken;
        public string[] httpToken = { };
        public int shardCount = 1;
        public int messageCacheSize = 100;
        public LogSeverity clientLogLevel = LogSeverity.Verbose;
        public LogSeverity commandLogLevel = LogSeverity.Verbose;
    }
}
