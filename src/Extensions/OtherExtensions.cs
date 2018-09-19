using System;
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


        /// <summary>Returns the given number rounded up to the nearest <see cref="int"/>.</summary>
        public static int Ceiling(this double num) => (int)Math.Ceiling(num);

        /// <summary>Returns the given number rounded down to the nearest <see cref="int"/>.</summary>
        public static int Floor(this double num) => (int)Math.Floor(num);


        /// <summary>Converts a <see cref="TimeSpan"/> into a string listing the days, hours and minutes.</summary>
        public static string Humanized(this TimeSpan span)
        {
            int days = (int)span.TotalDays, hours = span.Hours, minutes = span.Minutes;

            string result = $"{days} day{"s".If(days > 1)}, ".If(days > 0)
                          + $"{hours} hour{"s".If(hours > 1)}, ".If(hours > 0)
                          + $"{minutes} minute{"s".If(minutes > 1)}".If(minutes > 0);

            return result != "" ? result : "Just now";
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
