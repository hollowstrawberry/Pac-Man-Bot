using System;
using System.Collections.Generic;
using System.Threading;
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

        public List<Timer> timers; // If I ever want to schedule anything remotely using eval
        private readonly Timer deleteOldGames;


        public SchedulingService(DiscordShardedClient client, StorageService storage, LoggingService logger)
        {
            this.client = client;
            this.storage = storage;
            this.logger = logger;

            timers = new List<Timer>();
            deleteOldGames = new Timer(new TimerCallback(DeleteOldGames), null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(60));
        }


        public void DeleteOldGames(object state)
        {
            var now = DateTime.Now;
            int previousCount = storage.GameInstances.Count;

            var toDelete = storage.GameInstances.FindAll(game => (now - game.lastPlayed).TotalDays > 7.0);
            foreach (var game in toDelete) storage.DeleteGame(game);

            logger.Log(LogSeverity.Debug, LogSource.Scheduling, $"Cleaned {previousCount - storage.GameInstances.Count} abandoned games");
        }
    }
}
