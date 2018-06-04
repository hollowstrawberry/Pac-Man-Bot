using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace PacManBot
{
    public class CustomRandom : Random
    {
        private RNGCryptoServiceProvider crypto;

        public CustomRandom()
        {
            crypto = new RNGCryptoServiceProvider();
        }



        public override void NextBytes(byte[] buffer)
        {
            crypto.GetBytes(buffer);
        }

        public byte[] NextBytes(uint amount)
        {
            byte[] bytes = new byte[amount];
            NextBytes(bytes);
            return bytes;
        }


        public override int Next()
        {
            return Math.Abs(BitConverter.ToInt32(NextBytes(4), 0));
        }

        public override int Next(int max)
        {
            if (max < 0) throw new ArgumentOutOfRangeException(nameof(max));
            if (max == 0) return 0;

            return Next() % max;
        }

        public override int Next(int min, int max)
        {
            return Next(max - min) + min;
        }


        public override double NextDouble()
        {
            return BitConverter.ToUInt64(NextBytes(8), 0) / (1 << 11) / (Double)(1UL << 53);
        }

        public double NextDouble(double max)
        {
            if (max < 0) throw new ArgumentOutOfRangeException(nameof(max));
            if (max == 0) return 0;

            return NextDouble() * max;
        }

        public double NextDouble(double min, double max)
        {
            return NextDouble(max - min) + min;
        }



        public bool OneIn(int amount)
        {
            return Next(amount) == 0;
        }

        public T Choose<T>(IList<T> values)
        {
            return values[Next(values.Count)];
        }

        public void Shuffle<T>(IList<T> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = Next(n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }
    }
}
