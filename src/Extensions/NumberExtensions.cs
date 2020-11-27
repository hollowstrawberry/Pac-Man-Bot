using System;

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
    }
}
