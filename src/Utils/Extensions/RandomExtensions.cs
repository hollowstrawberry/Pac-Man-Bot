using System;
using System.Collections.Generic;

namespace PacManBot.Utils
{
    public static class RandomExtensions
    {
        public static double NextDouble(this Random random, double max)
        {
            if (max < 0) throw new ArgumentOutOfRangeException(nameof(max));
            if (max == 0) return 0;

            return random.NextDouble() * max;
        }


        public static double NextDouble(this Random random, double min, double max)
        {
            return random.NextDouble(max - min) + min;
        }


        public static bool OneIn(this Random random, int amount)
        {
            return random.Next(amount) == 0;
        }


        public static T Choose<T>(this Random random, IList<T> values)
        {
            if (values.Count == 0) return default;
            if (values.Count == 1) return values[0];

            return values[random.Next(values.Count)];
        }


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
