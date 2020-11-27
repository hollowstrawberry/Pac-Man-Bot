using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using PacManBot.Services;

namespace PacManBot.Extensions
{
    public static class MiscExtensions
    {
        /// <summary>Continues a task with an action to log exceptions if any were thrown.</summary>
        public static Task LogExceptions(this Task task, LoggingService log, string sourceMessage)
        {
            return task.ContinueWith(x => log.Exception(sourceMessage, x.Exception), TaskContinuationOptions.OnlyOnFaulted);
        }


        /// <summary>Sets an exit code and requests termination of the current application.</summary>
        public static void StopApplication(this IHostApplicationLifetime app, int exitCode = 0)
        {
            Environment.ExitCode = exitCode;
            app.StopApplication();
        }


        /// <summary>
        /// Converts a <see cref="TimeSpan"/> into a string listing days, hours, minutes and seconds.
        /// </summary>
        /// <param name="depth">How many units to show, from 1 (only days) to 4 (all)</param>
        /// <param name="empty">The default string if the timeframe is smaller than can be expressed.</param>
        public static string Humanized(this TimeSpan span, int depth = 4, string empty = "now")
        {
            int days = (int)span.TotalDays, hours = span.Hours, minutes = span.Minutes, seconds = span.Seconds;

            var units = new[] { (days, "day"), (hours, "hour"), (minutes, "minute"), (seconds, "second") };
            var text = new List<string>(4);

            foreach (var (val, name) in units.Take(depth))
            {
                if (val > 0) text.Add($"{val} {name}{"s".If(val > 1)}");
            }

            return text.Count > 0 ? text.JoinString(", ") : empty;
        }
    }
}
