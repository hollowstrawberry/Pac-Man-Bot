using System;
using System.Collections;
using System.Collections.Generic;
using PacManBot.Extensions;

namespace PacManBot.Utils
{
    /// <summary>
    /// A finite and ordered sequence of integers.
    /// </summary>
    public class Range : IEnumerable<int>
    {
        public int Start { get; } = 0;
        public int Stop { get; }
        public int Step { get; }

        public int Length => Math.Max(0, Step < 0 ? Start - Stop : Stop - Start) / Math.Abs(Step);


        /// <summary>Generates a sequence of integers starting at 0 and less than the given maximum.</summary>
        /// <param name="stop">The end point of the sequence, not included.</param>
        public Range(int stop) : this(0, stop, 1) { }

        /// <summary>Generates a sequence of integers in the specified range, 
        /// and with the given step between each element.</summary>
        /// <param name="start">The start point of the sequence.</param>
        /// <param name="stop">The end point of the sequence, not included.</param>
        /// <param name="step">The increment between each value in the sequence.</param>
        public Range(int start, int stop, int step = 1)
        {
            Step = step != 0 ? step : throw new ArgumentException("The step of the sequence cannot be 0.", nameof(step));
            Start = start;
            Stop = stop;
        }


        public IEnumerator<int> GetEnumerator()
        {
            for (int i = Start; Step < 0 ? i > Stop : i < Stop; i += Step)
                yield return i;

            yield break;
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();


        /// <summary>Displays the elements of the range in Python style.</summary>
        public override string ToString() => $"[{this.JoinString(", ")}]";

        public override bool Equals(object obj) => obj is Range range && Start == range.Start && Stop == range.Stop && Step == range.Step;

        public override int GetHashCode() => Start ^ Stop ^ (Step * (1 << 4));


        public static bool operator ==(Range left, Range right) => left.Equals(right);

        public static bool operator !=(Range left, Range right) => !left.Equals(right);
    }
}
