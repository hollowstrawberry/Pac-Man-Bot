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
        public event Func<Task> PrepareRestart;
        

        public SchedulingService(PmBotConfig config, LoggingService log, GameService games)
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
            foreach(var timer in Timers) timer.Dispose();
            Timers = new List<Timer>();
        }


        private async void DeleteOldGames(object state)
        {
            var now = DateTime.Now;
            int count = 0;

            var removedChannelGames = new List<IChannelGame>();

            var expiredGames = _games.AllGames
                .Where(g => now - g.LastPlayed > g.Expiry)
                .Where(g => g is not IUserGame) // The bot was offline for a long time and I don't want to delete pets
                .ToArray();
            foreach (var game in expiredGames)
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


            foreach (var game in removedChannelGames)
            {
                try
                {
                    await game.UpdateMessageAsync();
                }
                catch (NotFoundException) { }
            }
        }


        private async void RestartBot(object state)
        {
            _log.Info("Restarting");
            await PrepareRestart.Invoke();
            Environment.Exit(ExitCodes.ScheduledReboot);
        }
    }
}
