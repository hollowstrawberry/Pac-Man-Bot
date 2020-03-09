using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Discord;
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
        private readonly LoggingService log;
        private readonly StorageService storage;
        private readonly GameService games;
        private readonly InputService input;
        private readonly PmCommandService commands;
        private readonly SchedulingService schedule;

        public PmBot(PmConfig config, PmDiscordClient client, LoggingService log, StorageService storage,
            GameService games, InputService input, PmCommandService commands, SchedulingService schedule)
        {
            Config = config;
            this.client = client;
            this.log = log;
            this.storage = storage;
            this.games = games;
            this.input = input;
            this.commands = commands;
            this.schedule = schedule;
        }


        /// <summary>Starts the bot and its connection to Discord.</summary>
        public async Task StartAsync()
        {
            await commands.AddAllModulesAsync();
            await games.LoadGamesAsync();

            client.Log += log.ClientLog;
            client.AllShardsReady += ReadyAsync;

            await client.LoginAsync(TokenType.Bot, Config.discordToken);
            await client.StartAsync();
        }


        private async Task ReadyAsync()
        {
            log.Info("All shards ready");

            input.StartListening();
            schedule.StartTimers();

            schedule.PrepareRestart += StopAsync;
            client.ChannelDestroyed += OnChannelDestroyed;

            await client.SetStatusAsync(UserStatus.Online);
            await client.SetGameAsync($"with you!");


            if (File.Exists(Files.ManualRestart))
            {
                try
                {
                    ulong[] id = File.ReadAllText(Files.ManualRestart)
                        .Split("/").Select(ulong.Parse).ToArray();
                    File.Delete(Files.ManualRestart);

                    var message = await client.GetMessageChannel(id[0]).GetUserMessageAsync(id[1]);
                    await message.ModifyAsync(x => x.Content = CustomEmoji.Check);
                    log.Info("Resumed after manual restart");
                }
                catch (Exception e)
                {
                    log.Warning($"Resuming after manual restart: {e.Message}");
                }
            }
        }


        /// <summary>Safely stop most activity from the bot and disconnect from Discord.</summary>
        public async Task StopAsync()
        {
            await client.SetStatusAsync(UserStatus.DoNotDisturb); // why not

            input.StopListening();
            schedule.StopTimers();
            client.ChannelDestroyed -= OnChannelDestroyed;

            await Task.Delay(5_000); // Buffer time to finish up doing whatever

            await client.LogoutAsync();
            await client.StopAsync();

            log.Dispose();
        }
        

        private Task OnChannelDestroyed(SocketChannel channel)
        {
            games.Remove(games.GetForChannel(channel.Id));

            return Task.CompletedTask;
        }
    }
}
