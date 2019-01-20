using System;
using System.Collections.Generic;
using System.Linq;
using PacManBot.Extensions;

namespace PacManBot.Utils
{
    /// <summary>
    /// Holds a runtime cache of many traits about an enum type.
    /// </summary>
    /// <typeparam name="TEnum">The enum type whose traits you want to check.</typeparam>
    public static class EnumTraits<TEnum> where TEnum : struct, Enum
    {
        static EnumTraits()
        {
            Type = typeof(TEnum);
            UnderlyingType = Enum.GetUnderlyingType(Type);
            Values = Enum.GetValues(Type).Cast<TEnum>().Distinct().ToList().AsReadOnly();
            MinValue = Values.Min();
            MaxValue = Values.Max();
            Names = Enum.GetNames(Type).ToList().AsReadOnly();
            Dictionary = Names.ToDictionary(x => x, x => Convert.ToInt32(Enum.Parse<TEnum>(x))).AsReadOnly();
        }



        // I don't really use almost any of these but they're fun to have around

        /// <summary>The <see cref="System.Type"/> of the enum.</summary>
        public static Type Type { get; }

        /// <summary>The underlying <see cref="System.Type"/> behind the enum.</summary>
        public static Type UnderlyingType { get; }

        /// <summary>All distinct values contained in the enum.</summary>
        public static IReadOnlyList<TEnum> Values { get; }

        /// <summary>The minimum value of the enum.</summary>
        public static TEnum MinValue { get; }

        /// <summary>The maximum value of the enum.</summary>
        public static TEnum MaxValue { get; }

        /// <summary>All value names contained in the enum.</summary>
        public static IReadOnlyList<string> Names { get; }

        /// <summary>A dictionary containing all names and corresponding values contained in the enum.</summary>
        public static IReadOnlyDictionary<string, int> Dictionary { get; }
    }
}
