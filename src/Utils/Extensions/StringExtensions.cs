using System;
using System.Net;
using System.Text;

namespace PacManBot.Utils
{
    public static class StringExtensions
    {
        public static string If(this string text, bool condition) // Helps with complex text concatenation
        {
            return condition ? text : "";
        }


        public static string Truncate(this string text, int maxLength)
        {
            return text.Substring(0, Math.Min(maxLength, text.Length));
        }


        public static string Multiply(this string value, int amount)
        {
            if (amount == 1) return value;

            StringBuilder sb = new StringBuilder(amount * value.Length);
            for (int i = 0; i < amount; i++) sb.Append(value);
            return sb.ToString();
        }


        public static string Align(this object value, int length, bool right = false)
        {
            string str = value.ToString();
            string fill = new string(' ', Math.Max(0, length - str.Length));
            return right ? fill + str : str + fill;
        }

        public static string AlignTo(this object value, object guide, bool right = false)
        {
            return value.Align(guide.ToString().Length, right);
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
