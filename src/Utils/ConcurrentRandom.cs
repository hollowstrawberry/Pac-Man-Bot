using System;
using System.Security.Cryptography;

namespace PacManBot.Utils
{
    /// <summary>
    /// A wrapper for <see cref="RNGCryptoServiceProvider"/> with methods inherited from <see cref="Random"/>
    /// for ease of use. Main purpose is good randomness and thread-safety.
    /// </summary>
    public class ConcurrentRandom : Random
    {
        private readonly RNGCryptoServiceProvider _crypto;

        /// <summary>Creates a new instance.</summary>
        public ConcurrentRandom() : this(new RNGCryptoServiceProvider()) {}

        /// <summary>Creates a new instance that is a wrapper for the given <see cref="RNGCryptoServiceProvider"/>.</summary>
        public ConcurrentRandom(RNGCryptoServiceProvider crypto)
        {
            _crypto = crypto;
        }


        /// <summary>Fills the elements of a specified array of bytes with random numbers.</summary>
        public override void NextBytes(byte[] buffer)
        {
            _crypto.GetBytes(buffer);
        }


        /// <summary>Returns an array of bytes filled with random numbers.</summary>
        public byte[] NextBytes(uint amount)
        {
            byte[] bytes = new byte[amount];
            NextBytes(bytes);
            return bytes;
        }


        /// <summary>Returns a non-negative random integer.</summary>
        public override int Next()
        {
            return BitConverter.ToInt32(NextBytes(4), 0) & 0x7FFFFFFF; // Mask the most significant bit so that it's always positive
        }


        /// <summary>Returns a random integer that is greater than or equal to 0 and less than the given maximum.</summary>
        public override int Next(int max)
        {
            if (max < 0) throw new ArgumentOutOfRangeException(nameof(max));
            if (max == 0) return 0;

            return Next() % max;
        }


        /// <summary>Returns a random integer within the specified range, with an exclusive upper bound.</summary>
        public override int Next(int min, int max)
        {
            return Next(max - min) + min;
        }


        /// <summary>Returns a random floating-point number that is greater than or equal to 0.0 and less than 1.0.</summary>
        public override double NextDouble()
        {
            return BitConverter.ToUInt64(NextBytes(8), 0) / (1 << 11) / (double)(1UL << 53); // Voodoo magic
        }


        public override int GetHashCode() => HashCode.Combine(_crypto);
    }
}
