using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace PacManBot.Extensions
{
    public static class StringExtensions
    {
        public static string If(this string text, bool condition) // Helps with long text concatenation
        {
            return condition ? text : "";
        }


        public static string Truncate(this string text, int maxLength)
        {
            return text.Substring(0, Math.Min(maxLength, text.Length));
        }


        public static string TruncateStart(this string text, int maxLength)
        {
            return text.Substring(Math.Max(0, text.Length - maxLength));
        }


        public static string ReplaceMany(this string text, IEnumerable<KeyValuePair<string, string>> replacements)
        {
            var sb = new StringBuilder(text);
            foreach (var rep in replacements) sb.Replace(rep.Key, rep.Value);
            return sb.ToString();
        }


        public static string Multiply(this string value, int amount)
        {
            if (amount == 1) return value;

            StringBuilder sb = new StringBuilder(amount * value.Length);
            for (int i = 0; i < amount; i++) sb.Append(value);
            return sb.ToString();
        }


        public static string Align(this string value, int length, bool right = false)
        {
            string fill = new string(' ', Math.Max(0, length - value.Length));
            return right ? fill + value : value + fill;
        }

        public static string AlignTo(this string value, object guide, bool right = false)
        {
            return value.Align(guide.ToString().Length, right);
        }


        public static bool EndsOrStartsWith(this string text, string value)
        {
            return text.StartsWith(value) || text.EndsWith(value);
        }

        public static bool EndsOrStartsWith(this string text, string value, StringComparison comparisonType)
        {
            return text.StartsWith(value, comparisonType) || text.EndsWith(value, comparisonType);
        }


        public static bool ContainsAny(this string text, params string[] values)
        {
            foreach (string value in values)
            {
                if (text.Contains(value)) return true;
            }
            return false;
        }

        public static bool ContainsAny(this string text, params char[] values)
        {
            foreach (char value in values)
            {
                if (text.Contains(value.ToString())) return true;
            }
            return false;
        }


        public static bool IsImageUrl(this string url)
        {
            try
            {
                var req = WebRequest.Create(url);
                req.Method = "HEAD";
                using (var resp = req.GetResponse())
                {
                    return resp.ContentType.ToLower().StartsWith("image/");
                }
            }
            catch (WebException)
            {
                return false;
            }
        }
    }
}
