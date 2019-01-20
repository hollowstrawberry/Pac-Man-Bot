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
        private readonly LoggingService logger;
        private readonly GameService games;
        private readonly bool scheduledRestart;

        private CancellationTokenSource cancelShutdown = new CancellationTokenSource();

        /// <summary>All active scheduled actions.</summary>
        public List<Timer> timers;

        /// <summary>Fired when a scheduled restart is due.</summary>
        public event Func<Task> PrepareRestart;
        

        public SchedulingService(BotConfig config, DiscordShardedClient client, LoggingService logger, GameService games)
        {
            this.client = client;
            this.logger = logger;
            this.games = games;

            scheduledRestart = config.scheduledRestart;
        }


        /// <summary>Starts scheduling all predefined actions.</summary>
        public void StartTimers()
        {
            timers = new List<Timer>
            {
                new Timer(CheckConnection, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(10)),
                new Timer(DeleteOldGames, null, TimeSpan.Zero, TimeSpan.FromSeconds(10))
            };

            if (scheduledRestart)
            {
                TimeSpan timeToGo = TimeSpan.FromDays(1) - DateTime.Now.TimeOfDay;
                if (timeToGo < TimeSpan.FromMinutes(60)) timeToGo += TimeSpan.FromDays(1);

                timers.Add(new Timer(RestartBot, null, timeToGo, Timeout.InfiniteTimeSpan));
            }

            client.ShardConnected += OnShardConnected;
        }


        /// <summary>Cease all scheduled actions</summary>
        public void StopTimers()
        {
            client.ShardConnected -= OnShardConnected;

            cancelShutdown.Cancel();
            cancelShutdown = new CancellationTokenSource();

            foreach(var timer in timers) timer.Dispose();
            timers = new List<Timer>();
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



        private async void CheckConnection(object state)
        {
            if (client.AllShardsConnected()) return;

            await logger.Log(LogSeverity.Info, LogSource.Scheduling, "A shard is disconnected. Waiting for reconnection...");

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(2), cancelShutdown.Token);
                await logger.Log(LogSeverity.Critical, LogSource.Scheduling, "Reconnection timed out. Shutting down");
                Environment.Exit(ExitCodes.ReconnectionTimeout);
            }
            catch (OperationCanceledException)
            {
                await logger.Log(LogSeverity.Info, LogSource.Scheduling, "All shards reconnected. Shutdown aborted");
            }
        }


        private async void DeleteOldGames(object state)
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


        private async void RestartBot(object state)
        {
            await logger.Log(LogSeverity.Info, LogSource.Scheduling, "Preparing to restart");
            await PrepareRestart.Invoke();
            await logger.Log(LogSeverity.Info, LogSource.Scheduling, "Restarting");
            Environment.Exit(ExitCodes.ScheduledReboot);
        }
    }
}
