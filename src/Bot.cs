using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Discord;
using Discord.WebSocket;
using PacManBot.Services;
using PacManBot.Constants;

namespace PacManBot
{
    public class Bot
    {
        private BotConfig botConfig;
        private IServiceProvider provider;

        private DiscordShardedClient client;
        private LoggingService logger;
        private StorageService storage;

        private CancellationTokenSource cancelReconnectTimeout = null;
        private Stopwatch guildCountTimer = null;
        int shardsReady = 0;


        public Bot(BotConfig botConfig, IServiceProvider provider)
        {
            this.botConfig = botConfig;
            this.provider = provider;

            client = provider.GetRequiredService<DiscordShardedClient>();
            logger = provider.GetRequiredService<LoggingService>();
            storage = provider.GetRequiredService<StorageService>();

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
            await client.LoginAsync(TokenType.Bot, botConfig.discordToken); //Login to discord
            await client.StartAsync(); //Connect to the websocket

            provider.GetRequiredService<CommandHandler>();
            provider.GetRequiredService<ReactionHandler>();
            provider.GetRequiredService<ScriptingService>();
        }





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
