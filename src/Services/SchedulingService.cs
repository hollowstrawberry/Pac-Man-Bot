using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Discord.WebSocket;
using PacManBot.Constants;
using PacManBot.Extensions;

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


        private async void RestartBot(object state)
        {
            log.Info("Restarting", LogSource.Scheduling);
            await PrepareRestart.Invoke();
            Environment.Exit(ExitCodes.ScheduledReboot);
        }
    }
}
