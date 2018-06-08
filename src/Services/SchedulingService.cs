using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Discord;
using Discord.WebSocket;
using PacManBot.Constants;

namespace PacManBot.Services
{
    public class SchedulingService
    {
        private readonly DiscordShardedClient client;
        private readonly StorageService storage;
        private readonly LoggingService logger;

        public List<Timer> timers; // If for some reason I ever want to schedule anything remotely using eval
        private readonly Timer checkConnection;
        private readonly Timer deleteOldGames;

        private CancellationTokenSource cancelShutdown = new CancellationTokenSource();


        public SchedulingService(DiscordShardedClient client, StorageService storage, LoggingService logger)
        {
            this.client = client;
            this.storage = storage;
            this.logger = logger;

            checkConnection = new Timer(new TimerCallback(CheckConnection), null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
            deleteOldGames = new Timer(new TimerCallback(DeleteOldGames), null, TimeSpan.Zero, TimeSpan.FromMinutes(30));

            timers = new List<Timer>();
            timers.Append(checkConnection);
            timers.Append(deleteOldGames);

            //Events
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



        public async void CheckConnection(object state)
        {
            if (client.AllShardsConnected()) return;

            await logger.Log(LogSeverity.Info, LogSource.Scheduling, "A shard is disconnected. Waiting for reconnection...");

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(2), cancelShutdown.Token);
                await logger.Log(LogSeverity.Critical, LogSource.Scheduling, "Reconnection timed out. Shutting down...");
                Environment.Exit(666);
            }
            catch (OperationCanceledException)
            {
                await logger.Log(LogSeverity.Info, LogSource.Scheduling, "All shards reconnected. Shutdown aborted");
            }
        }


        public void DeleteOldGames(object state)
        {
            var now = DateTime.Now;
            int previousCount = storage.Games.Count;

            foreach (var game in storage.Games.Where(g => (now - g.LastPlayed) > g.Expiry).ToArray())
            {
                storage.DeleteGame(game);
            }

            int removed = previousCount - storage.Games.Count;
            if (removed > 0) logger.Log(LogSeverity.Info, LogSource.Scheduling, $"Removed {removed} expired game{"s".If(removed > 1)}");
        }
    }
}
