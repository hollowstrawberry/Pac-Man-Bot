using System;
using System.Collections.Generic;
using System.Linq;
using PacManBot.Extensions;

namespace PacManBot.Utils
{
    public static class EnumTraits<TEnum> where TEnum : struct, Enum
    {
        static EnumTraits()
        {
            Type = typeof(TEnum);
            Values = Enum.GetValues(Type).Cast<TEnum>().Distinct().ToList().AsReadOnly();
            MinValue = Values.Min();
            MaxValue = Values.Max();
            Names = Enum.GetNames(Type).ToList().AsReadOnly();
            Dictionary = Names.ToDictionary(x => x, x => Convert.ToInt32(Enum.Parse<TEnum>(x))).AsReadOnly();
        }


        // I don't use almost any of these but they're fun to have around
        public static Type Type { get; }
        public static IReadOnlyList<TEnum> Values { get; }
        public static TEnum MinValue { get; }
        public static TEnum MaxValue { get; }
        public static IReadOnlyList<string> Names { get; }
        public static IReadOnlyDictionary<string, int> Dictionary { get; }
    }
}
