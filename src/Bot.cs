using System;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Serialization;
using Discord;
using Discord.WebSocket;
using PacManBot.Services;

namespace PacManBot
{
    [DataContract]
    public class BotConfig
    {
        [DataMember] public readonly string defaultPrefix = "<";
        [DataMember] public readonly string discordToken;
        [DataMember] public readonly string[] httpToken = { };
        [DataMember] public readonly int shardCount = 1;
        [DataMember] public readonly int messageCacheSize = 100;
        [DataMember] public readonly LogSeverity clientLogLevel = LogSeverity.Verbose;
        [DataMember] public readonly LogSeverity commandLogLevel = LogSeverity.Verbose;
    }


    public class Bot
    {
        BotConfig botConfig;
        DiscordShardedClient client;
        LoggingService logger;
        StorageService storage;

        int shardsReady = 0;
        DateTime lastGuildCountUpdate = DateTime.MinValue;


        public Bot(BotConfig botConfig, IServiceProvider services)
        {
            this.botConfig = botConfig;
            client = services.Get<DiscordShardedClient>();
            logger = services.Get<LoggingService>();
            storage = services.Get<StorageService>();

            //Events
            client.ShardReady += OnShardReady;
            client.JoinedGuild += OnJoinedGuild;
            client.LeftGuild += OnLeftGuild;
            client.ChannelDestroyed += OnChannelDestroyed;
        }


        public async Task StartAsync()
        {
            await client.LoginAsync(TokenType.Bot, botConfig.discordToken);
            await client.StartAsync();
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
            _ = UpdateGuildCountAsync();

            foreach (var game in storage.GameInstances.Where(g => g.Guild?.Id == guild.Id).ToArray())
            {
                storage.DeleteGame(game);
            }

            return Task.CompletedTask;
        }


        private Task OnChannelDestroyed(SocketChannel channel)
        {
            foreach (var game in storage.GameInstances.Where(g => g.channelId == channel.Id).ToArray())
            {
                storage.DeleteGame(game);
            }

            return Task.CompletedTask;
        }




        private async Task UpdateGuildCountAsync()
        {
            try // I have to wrap discarded async methods in a try block so that exceptions don't go silent
            {
                var now = DateTime.Now;

                if ((now - lastGuildCountUpdate).TotalMinutes > 20.0)
                {
                    lastGuildCountUpdate = now;
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

                    await logger.Log(LogSeverity.Info, $"Guild count updated to {guilds}");
                }
            }
            catch (Exception e)
            {
                await logger.Log(LogSeverity.Error, $"{e}");
            }
        }
    }
}
