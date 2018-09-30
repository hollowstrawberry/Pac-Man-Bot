using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using PacManBot.Utils;

namespace PacManBot.Extensions
{
    public static class OtherExtensions
    {
        /// <summary>Gets a service of type <typeparamref name="T"/> from a <see cref="IServiceProvider"/>. 
        /// Throws an exception if one is not found. I didn't like the long name of the original.</summary>
        [DebuggerStepThrough]
        public static T Get<T>(this IServiceProvider provider) => provider.GetRequiredService<T>();


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
        /// <paramref name="depth"/> can be specified to limit how many units to show.
        /// </summary>
        public static string Humanized(this TimeSpan span, int depth = 4)
        {
            int days = (int)span.TotalDays, hours = span.Hours, minutes = span.Minutes, seconds = span.Seconds;

            var units = new[] { (days, "day"), (hours, "hour"), (minutes, "minute"), (seconds, "second") };
            var text = new List<string>(4);

            foreach (var (val, name) in units.Take(depth))
            {
                if (val > 0) text.Add($"{val} {name}{"s".If(val > 1)}");
            }

            return text.Count > 0 ? text.JoinString(", ") : "Just now";
        }


        /// <summary>Gets a string that describes the timeframe when an event occured given a <see cref="TimePeriod"/>.</summary>
        public static string Humanized(this TimePeriod period)
        {
            switch (period)
            {
                case TimePeriod.Month: return "in the last 30 days";
                case TimePeriod.Week: return "in the last 7 days";
                case TimePeriod.Day: return "in the last 24 hours";
                default: return "of all time";
            }
        }
    }
}
