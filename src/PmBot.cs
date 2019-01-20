using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Discord;
using Discord.Net;
using Discord.WebSocket;
using PacManBot.Constants;
using PacManBot.Extensions;
using PacManBot.Services;

namespace PacManBot
{
    /// <summary>
    /// Runs an instance of Pac-Man Bot and handles its connection to Discord.
    /// </summary>
    public class PmBot
    {
        public static readonly RequestOptions DefaultOptions = new RequestOptions {
            RetryMode = RetryMode.RetryRatelimit,
            Timeout = 10000
        };

        /// <summary>Runtime configuration of the bot.</summary>
        public PmConfig Config { get; }

        private readonly PmDiscordClient client;
        private readonly LoggingService logger;
        private readonly StorageService storage;
        private readonly GameService games;
        private readonly InputService input;
        private readonly SchedulingService schedule;

        private int shardsReady;
        private DateTime lastGuildCountUpdate = DateTime.MinValue;


        public PmBot(PmConfig config, PmDiscordClient client, LoggingService logger,
            StorageService storage, GameService games, InputService input, SchedulingService schedule)
        {
            Config = config;
            this.client = client;
            this.logger = logger;
            this.storage = storage;
            this.games = games;
            this.input = input;
            this.schedule = schedule;
        }


        /// <summary>Starts the bot's connection to Discord.</summary>
        public async Task StartAsync()
        {
            client.ShardReady += OnShardReady;
            await client.LoginAsync(TokenType.Bot, Config.discordToken);
            await client.StartAsync();
        }


        private async Task ReadyAsync()
        {
            input.StartListening();
            schedule.StartTimers();
            schedule.PrepareRestart += StopAsync;
            client.JoinedGuild += OnJoinedGuild;
            client.LeftGuild += OnLeftGuild;
            client.ChannelDestroyed += OnChannelDestroyed;

            UpdateGuildCount();

            if (File.Exists(Files.ManualRestart))
            {
                try
                {
                    ulong[] id = File.ReadAllText(Files.ManualRestart)
                        .Split("/").Select(ulong.Parse).ToArray();
                    File.Delete(Files.ManualRestart);

                    var message = await client.GetMessageChannel(id[0]).GetUserMessageAsync(id[1]);
                    await message.ModifyAsync(x => x.Content = CustomEmoji.Check);
                }
                catch (HttpException e)
                {
                    await logger.Log(LogSeverity.Error, $"{e}");
                }
            }
        }


        /// <summary>Safely stop most activity from the bot and disconnect from Discord.</summary>
        public async Task StopAsync()
        {
            input.StopListening();
            schedule.StopTimers();
            client.ShardReady -= OnShardReady;
            client.JoinedGuild -= OnJoinedGuild;
            client.LeftGuild -= OnLeftGuild;
            client.ChannelDestroyed -= OnChannelDestroyed;

            await Task.Delay(5_000); // Buffer time to finish up doing whatever

            await client.LogoutAsync();
            await client.StopAsync();
        }




        private async Task OnShardReady(DiscordSocketClient shard)
        {
            if (++shardsReady == client.Shards.Count)
            {
                if (client.Shards.Count > 1) await logger.Log(LogSeverity.Info, "All shards ready");
                await ReadyAsync();
            }
        }


        private Task OnJoinedGuild(SocketGuild guild)
        {
            UpdateGuildCount();
            return Task.CompletedTask;
        }


        private Task OnLeftGuild(SocketGuild guild)
        {
            UpdateGuildCount();
            return Task.CompletedTask;
        }


        private Task OnChannelDestroyed(SocketChannel channel)
        {
            var game = games.GetForChannel(channel.Id);
            if (game != null) games.Remove(game);

            return Task.CompletedTask;
        }




        private async void UpdateGuildCount()
        {
            try
            {
                await InnerUpdateGuildCount();
            }
            catch (Exception e)
            {
                await logger.Log(LogSeverity.Error, $"{e}");
            }
        }

        private async Task InnerUpdateGuildCount()
        {
            var now = DateTime.Now;
            if ((now - lastGuildCountUpdate).TotalMinutes < 20.0) return;

            lastGuildCountUpdate = now;
            int guilds = client.Guilds.Count;
            await client.SetGameAsync($"{Config.defaultPrefix}help | {guilds} guilds");

            if (Config.httpDomain.Length == 0 || Config.httpToken.Length == 0) return;

            using (var httpClient = new HttpClient()) // Update bot list websites
            {
                for (int i = 0; i < Config.httpDomain.Length && i < Config.httpToken.Length; i++)
                {
                    string requesturi = Config.httpDomain[i].Replace("{id}", $"{client.CurrentUser.Id}");

                    var content = new StringContent(
                        $"{{\"server_count\": {guilds}}}", System.Text.Encoding.UTF8, "application/json");

                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(Config.httpToken[i]);
                    var response = await httpClient.PostAsync(requesturi, content);

                    await logger.Log(
                        response.IsSuccessStatusCode ? LogSeverity.Verbose : LogSeverity.Warning,
                        $"Sent guild count to {requesturi} - {(response.IsSuccessStatusCode ? "Success" : $"Response:\n{response}")}");
                }
            }

            await logger.Log(LogSeverity.Info, $"Guild count updated to {guilds}");
        }
    }
}
