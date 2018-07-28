using System;
using System.Collections.Generic;

namespace PacManBot.Extensions
{
    public static class RandomExtensions
    {
        /// <summary>Returns a random floating-point number that is greater than or equal to 0.0, 
        /// and less than <paramref name="max"/>.</summary>
        public static double NextDouble(this Random random, double max)
        {
            if (max < 0) throw new ArgumentOutOfRangeException(nameof(max));
            if (max == 0) return 0;

            return random.NextDouble() * max;
        }


        /// <summary>Returns a random floating-point number that is greater than or equal to <paramref name="min"/>, 
        /// and less than <paramref name="max"/>.</summary>
        public static double NextDouble(this Random random, double min, double max)
        {
            return random.NextDouble(max - min) + min;
        }


        /// <summary>Returns true with a one in <paramref name="amount"/> chance, otherwise false.</summary>
        public static bool OneIn(this Random random, int amount)
        {
            return random.Next(amount) == 0;
        }


        /// <summary>Returns a random element from the given list.</summary>
        public static T Choose<T>(this Random random, IReadOnlyList<T> values)
        {
            switch (values.Count)
            {
                case 0: throw new InvalidOperationException("Can't choose an element from an empty list.");
                case 1: return values[0];
                default: return values[random.Next(values.Count)];
            }
        }


        /// <summary>Performs a fair Fisher-Yates style shuffling on the given list's elements.</summary>
        public static void Shuffle<T>(this Random random, IList<T> list)
        {
            int n = list.Count - 1;
            while (n > 0)
            {
                list.Swap(n, random.Next(n + 1));
                n--;
            }
        }
    }
}
