using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Net;
using Discord.WebSocket;
using PacManBot.Constants;
using PacManBot.Extensions;
using PacManBot.Games;

namespace PacManBot.Services
{

    /// <summary>
    /// Routinely executes specific actions such as connection checks.
    /// </summary>
    public class SchedulingService
    {
        private readonly PmDiscordClient client;
        private readonly LoggingService log;
        private readonly GameService games;
        private readonly bool scheduledRestart;

        private CancellationTokenSource cancelShutdown = new CancellationTokenSource();

        /// <summary>All active scheduled actions.</summary>
        public List<Timer> timers;

        /// <summary>Fired when a scheduled restart is due.</summary>
        public event Func<Task> PrepareRestart;
        

        public SchedulingService(PmConfig config, PmDiscordClient client, LoggingService log, GameService games)
        {
            this.client = client;
            this.log = log;
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

            log.Info("A shard is disconnected. Waiting for reconnection...", LogSource.Scheduling);

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(2), cancelShutdown.Token);
                log.Fatal("Reconnection timed out. Shutting down", LogSource.Scheduling);
                Environment.Exit(ExitCodes.ReconnectionTimeout);
            }
            catch (OperationCanceledException)
            {
                log.Info("All shards reconnected. Shutdown aborted", LogSource.Scheduling);
            }
        }


        private async void DeleteOldGames(object state)
        {
            var now = DateTime.Now;
            int count = 0;

            var removedChannelGames = new List<IChannelGame>();

            var expiredGames = games.AllGames
                .Where(g => now - g.LastPlayed > g.Expiry)
                .Where(g => !(g is IUserGame)) // The bot was offline for a long time and I don't want to delete pets
                .ToArray();
            foreach (var game in expiredGames)
            {
                count++;
                game.State = GameState.Cancelled;
                games.Remove(game, false);

                if (game is IChannelGame cGame) removedChannelGames.Add(cGame);
            }


            if (count > 0)
            {
                log.Info($"Removed {count} expired game{"s".If(count > 1)}", LogSource.Scheduling);
            }


            if (client?.LoginState == LoginState.LoggedIn)
            {
                foreach (var game in removedChannelGames)
                {
                    try
                    {
                        var gameMessage = await game.GetMessageAsync();
                        if (gameMessage != null) await gameMessage.ModifyAsync(game.GetMessageUpdate(), PmBot.DefaultOptions);
                    }
                    catch (HttpException) { } // Something happened to the message, we can ignore it
                }
            }
        }


        private async void RestartBot(object state)
        {
            log.Info("Restarting", LogSource.Scheduling);
            await PrepareRestart.Invoke();
            Environment.Exit(ExitCodes.ScheduledReboot);
        }
    }
}
