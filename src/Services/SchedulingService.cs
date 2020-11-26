using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Exceptions;
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
        private readonly LoggingService _log;
        private readonly GameService _games;
        private readonly bool _scheduledRestart;


        /// <summary>All active scheduled actions.</summary>
        public List<Timer> Timers { get; private set; }

        /// <summary>Fired when a scheduled restart is due.</summary>
        public event Func<CancellationToken, Task> PrepareRestart;
        

        public SchedulingService(BotConfig config, LoggingService log, GameService games)
        {
            _log = log;
            _games = games;

            _scheduledRestart = config.scheduledRestart;
        }


        /// <summary>Starts scheduling all predefined actions.</summary>
        public void StartTimers()
        {
            Timers = new List<Timer>
            {
                new Timer(DeleteOldGames, null, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(60))
            };

            if (_scheduledRestart)
            {
                TimeSpan timeToGo = TimeSpan.FromDays(1) - DateTime.Now.TimeOfDay;
                if (timeToGo < TimeSpan.FromMinutes(60)) timeToGo += TimeSpan.FromDays(1);

                Timers.Add(new Timer(RestartBot, null, timeToGo, Timeout.InfiniteTimeSpan));
            }
        }


        /// <summary>Cease all scheduled actions</summary>
        public void StopTimers()
        {
            if (Timers == null) return;
            foreach(var timer in Timers) timer.Dispose();
            Timers = new List<Timer>();
        }


        private void DeleteOldGames(object state)
        {
            var now = DateTime.Now;
            int count = 0;
            var removedChannelGames = new List<IChannelGame>();

            foreach (var game in _games.AllGames.Where(g => now - g.LastPlayed > g.Expiry).ToArray())
            {
                count++;
                game.State = GameState.Cancelled;
                _games.Remove(game, false);
                if (game is IChannelGame cGame) removedChannelGames.Add(cGame);
            }

            if (count > 0)
            {
                _log.Debug($"Removed {count} expired game{"s".If(count > 1)}");
            }

            if (removedChannelGames.Count is > 0 and < 10)
            {
                Task.Run(async () =>
                {
                    foreach (var game in removedChannelGames)
                    {
                        try { await game.UpdateMessageAsync(); }
                        catch { }
                    }
                });
            }
        }


        private async void RestartBot(object state)
        {
            _log.Info("Restarting");
            await PrepareRestart.Invoke(CancellationToken.None).LogExceptions(_log, "Restarting");
            Environment.Exit(ExitCodes.ScheduledReboot);
        }
    }
}
