using System;
using System.Collections.Generic;
using System.Linq;

namespace PacManBot.Extensions
{
    public static class NumberExtensions
    {
        /// <summary>Returns the given number rounded up to a <see cref="int"/>.</summary>
        public static int Ceiling(this double num) => (int)Math.Ceiling(num);

        /// <summary>Returns the given number rounded down to an <see cref="int"/>.</summary>
        public static int Floor(this double num) => (int)Math.Floor(num);

        /// <summary>Returns the given number rounded to a number of decimals.</summary>
        public static double Round(this double num, int decimals) => Math.Round(num, decimals);

        /// <summary>Returns the given number rounded up or down to the nearest <see cref="int"/>.</summary>
        public static int Round(this double num) => (int)Math.Round(num);


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
