using System;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Headers;
using Discord;
using Discord.WebSocket;
using PacManBot.Utils;
using PacManBot.Services;
using PacManBot.Extensions;

namespace PacManBot
{
    /// <summary>
    /// Runs an instance of this Discord bot and handles most events fired by the Discord.Net API.
    /// </summary>
    public class Bot
    {
        public static readonly ConcurrentRandom Random = new ConcurrentRandom();
        public static readonly RequestOptions DefaultOptions = new RequestOptions {
            RetryMode = RetryMode.RetryRatelimit,
            Timeout = 10000
        };

        private readonly BotConfig botConfig;
        private readonly DiscordShardedClient client;
        private readonly LoggingService logger;
        private readonly StorageService storage;
        private readonly GameService games;

        private int shardsReady;
        private DateTime lastGuildCountUpdate = DateTime.MinValue;


        public Bot(BotConfig botConfig, IServiceProvider services)
        {
            this.botConfig = botConfig;
            client = services.Get<DiscordShardedClient>();
            logger = services.Get<LoggingService>();
            storage = services.Get<StorageService>();
            games = services.Get<GameService>();

            games.LoadGames(services);

            // Events
            client.ShardReady += OnShardReady;
            client.JoinedGuild += OnJoinedGuild;
            client.LeftGuild += OnLeftGuild;
            client.ChannelDestroyed += OnChannelDestroyed;
        }


        /// <summary>Starts the bot's connection to Discord.</summary>
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
            return Task.CompletedTask;
        }


        private Task OnChannelDestroyed(SocketChannel channel)
        {
            var game = games.GetForChannel(channel.Id);
            if (game != null) games.Remove(game);

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
                        string[] website = { "bots.discord.pw", "discordbots.org" };
                        for (int i = 0; i < website.Length && i < botConfig.httpToken.Length; i++)
                        {
                            if (string.IsNullOrWhiteSpace(botConfig.httpToken[i])) continue;

                            string requesturi = $"https://{website[i]}/api/bots/{client.CurrentUser.Id}/stats";

                            var content = new StringContent(
                                $"{{\"server_count\": {guilds}}}", System.Text.Encoding.UTF8, "application/json");

                            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(botConfig.httpToken[i]);
                            var response = await httpClient.PostAsync(requesturi, content);

                            await logger.Log(
                                response.IsSuccessStatusCode ? LogSeverity.Verbose : LogSeverity.Warning,
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
