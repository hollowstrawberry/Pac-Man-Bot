using System;
using System.Collections.Generic;
using System.Linq;
using PacManBot.Extensions;

namespace PacManBot.Utils
{
    public static class Enum<TEnum> where TEnum : struct, IConvertible
    {
        private static TEnum? _minValue = null;
        private static TEnum? _maxValue = null;
        private static IReadOnlyList<TEnum> _values = null;
        private static IReadOnlyList<string> _names = null;
        private static IReadOnlyDictionary<string, int> _dictionary = null;


        public static Type Type { get; } = AssertEnumType();

        private static Type AssertEnumType()
        {
            Type type = typeof(TEnum);
            if (!type.IsEnum) throw new InvalidOperationException($"Type {type.Name} is not an Enum");
            return type;
        }



        public static IReadOnlyList<TEnum> Values
        {
            get
            {
                if (_values == null) _values = Enum.GetValues(Type).Cast<TEnum>().Distinct().ToList().AsReadOnly();
                return _values;
            }
        }


        public static IReadOnlyList<string> Names
        {
            get
            {
                if (_names == null) _names = Enum.GetNames(Type).ToList().AsReadOnly();
                return _names;
            }
        }


        public static IReadOnlyDictionary<string, int> Dictionary
        {
            get
            {
                if (_dictionary == null) _dictionary = Names.ToDictionary(x => x, x => Convert.ToInt32(Enum.Parse<TEnum>(x))).AsReadOnly();
                return _dictionary;
            }
        }


        public static TEnum MinValue
        {
            get
            {
                if (!_minValue.HasValue) _minValue = Values.Min();
                return _minValue.Value;
            }
        }


        public static TEnum MaxValue
        {
            get
            {
                if (!_maxValue.HasValue) _maxValue = Values.Max();
                return _maxValue.Value;
            }
        }
    }
}
