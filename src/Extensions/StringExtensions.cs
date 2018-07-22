using System;
using System.Net;
using System.Text;
using System.Linq;
using System.Collections.Generic;

namespace PacManBot.Extensions
{
    public static class StringExtensions
    {
        /// <summary>Returns the given text if the condition is true, otherwise returns an empty string.
        /// Helps a little with long text concatenation.</summary>
        public static string If(this string text, bool condition)
            => condition ? text : "";


        /// <summary>Retrieves a string with the end trimmed off if the original exceeds the provided maximum length.</summary>
        public static string Truncate(this string value, int maxLength)
            => value.Substring(0, Math.Min(maxLength, value.Length));


        /// <summary>Retrieves a string with the beginning trimmed off if the original exceeds the provided maximum length.</summary>
        public static string TruncateStart(this string value, int maxLength)
            => value.Substring(Math.Max(0, value.Length - maxLength));


        /// <summary>Determines whether the beginning or end of a string matches the specified value.</summary>
        public static bool StartsOrEndsWith(this string text, string value)
            => text.StartsWith(value) || text.EndsWith(value);


        /// <summary>Determines whether the beginning or end of a string matches the specified value
        /// when compared using the specified comparison option.</summary>
        public static bool StartsOrEndsWith(this string text, string value, StringComparison comparisonType)
            => text.StartsWith(value, comparisonType) || text.EndsWith(value, comparisonType);


        /// <summary>Whether any of the provided values occur within a string.</summary>
        public static bool ContainsAny(this string text, params string[] values)
            => values.Any(text.Contains);


        /// <summary>Whether any of the provided characters occur within a string.</summary>
        public static bool ContainsAny(this string text, params char[] values)
            => values.Select(x => x.ToString()).Any(text.Contains);


        /// <summary>Replaces all occurrences of the specified "before" values,
        /// and replaces them with their corresponding "after" values, inside a string.</summary>
        public static string ReplaceMany(this string text, params (string before, string after)[] replacements)
        {
            var sb = new StringBuilder(text);
            foreach (var (before, after) in replacements) sb.Replace(before, after);
            return sb.ToString();
        }


        /// <summary>Returns a string with the original value repeated the specified amount of times.</summary>
        public static string Multiply(this string value, int amount)
        {
            if (amount == 1) return value;

            var sb = new StringBuilder(amount * value.Length);
            for (int i = 0; i < amount; i++) sb.Append(value);
            return sb.ToString();
        }
    }
}
