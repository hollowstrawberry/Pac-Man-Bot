using System;
using System.Security.Cryptography;

namespace PacManBot.Utils
{
    public class ThreadSafeRandom : Random
    {
        private RNGCryptoServiceProvider crypto;

        public ThreadSafeRandom()
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
            return BitConverter.ToInt32(NextBytes(4), 0) & 0x7FFFFFFF; // Least significant bit always off, positive
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
            return BitConverter.ToUInt64(NextBytes(8), 0) / (1 << 11) / (Double)(1UL << 53); // Voodoo magic
        }


        public override int GetHashCode()
        {
            return crypto.GetHashCode();
        }
    }
}
