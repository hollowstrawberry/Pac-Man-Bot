using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Discord;
using Discord.Net;
using Discord.WebSocket;
using PacManBot.Games;
using PacManBot.Constants;
using PacManBot.Extensions;

namespace PacManBot.Services
{
    /// <summary>
    /// Routinely executes specific actions such as connection checks.
    /// </summary>
    public class SchedulingService
    {
        private readonly DiscordShardedClient client;
        private readonly StorageService storage;
        private readonly LoggingService logger;
        private readonly InputService input;
        private readonly GameService games;

        public List<Timer> timers;
        private CancellationTokenSource cancelShutdown = new CancellationTokenSource();


        public SchedulingService(IServiceProvider services)
        {
            client = services.Get<DiscordShardedClient>();
            storage = services.Get<StorageService>();
            logger = services.Get<LoggingService>();
            games = services.Get<GameService>();
            input = services.Get<InputService>();
            var config = services.Get<BotConfig>();

            timers = new List<Timer>
            {
                new Timer(CheckConnection, null, TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10)),
                new Timer(DeleteOldGames, null, TimeSpan.Zero, TimeSpan.FromSeconds(10))
            };

            if (config.scheduledRestart)
            {
                TimeSpan timeToGo = TimeSpan.FromDays(1) - DateTime.Now.TimeOfDay;
                if (timeToGo < TimeSpan.FromMinutes(60)) timeToGo += TimeSpan.FromDays(1);

                timers.Add(new Timer(RestartBot, null, timeToGo, Timeout.InfiniteTimeSpan));
            }

            client.ShardConnected += OnShardConnected;
        }



        private Task OnShardConnected(DiscordSocketClient shard)
        {
            if (client.AllShardsConnected())
            {
                cancelShutdown.Cancel();
                cancelShutdown = new CancellationTokenSource();
            }
            return Task.CompletedTask;
        }



        /// <summary>Scheduled action that ensures the connection to Discord and, if not, prepares a reboot.</summary>
        public async void CheckConnection(object state)
        {
            if (client.AllShardsConnected()) return;

            await logger.Log(LogSeverity.Info, LogSource.Scheduling, "A shard is disconnected. Waiting for reconnection...");

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(2), cancelShutdown.Token);
                await logger.Log(LogSeverity.Critical, LogSource.Scheduling, "Reconnection timed out. Shutting down...");
                Environment.Exit(ExitCodes.ReconnectionTimeout);
            }
            catch (OperationCanceledException)
            {
                await logger.Log(LogSeverity.Info, LogSource.Scheduling, "All shards reconnected. Shutdown aborted");
            }
        }


        /// <summary>Scheduled action that scans existing games and removes expired ones.</summary>
        public async void DeleteOldGames(object state)
        {
            var now = DateTime.Now;
            int count = 0;

            var removedChannelGames = new List<IChannelGame>();

            var expiredGames = games.AllGames.Where(g => now - g.LastPlayed > g.Expiry).ToArray();
            foreach (var game in expiredGames)
            {
                count++;
                game.State = State.Cancelled;
                games.Remove(game, false);

                if (game is IChannelGame cGame) removedChannelGames.Add(cGame);
            }


            if (count > 0)
            {
                await logger.Log(LogSeverity.Info, LogSource.Scheduling, $"Removed {count} expired game{"s".If(count > 1)}");
            }


            if (client?.LoginState == LoginState.LoggedIn)
            {
                foreach (var game in removedChannelGames)
                {
                    try
                    {
                        var gameMessage = await game.GetMessage();
                        if (gameMessage != null) await gameMessage.ModifyAsync(game.GetMessageUpdate(), Bot.DefaultOptions);
                    }
                    catch (HttpException) { } // Something happened to the message, we can ignore it
                }
            }
        }


        /// <summary>Scheduled task that prepares a safe shutdown in order to restart.</summary>
        public async void RestartBot(object state)
        {
            input.StopListening();

            // Just waits a bit to finish up whatever it might be doing at the moment
            await logger.Log(LogSeverity.Info, LogSource.Scheduling, "Preparing to shut down.");
            await Task.Delay(10_000);
            await logger.Log(LogSeverity.Info, LogSource.Scheduling, "Shutting down.");
            Environment.Exit(ExitCodes.ScheduledReboot);
        }
    }
}
