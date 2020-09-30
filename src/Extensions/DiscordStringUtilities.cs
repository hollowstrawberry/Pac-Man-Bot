using System;
using System.Drawing;
using System.Text.RegularExpressions;
using DSharpPlus.Entities;
using PacManBot.Services;

namespace PacManBot.Extensions
{
    public static class DiscordStringUtilities
    {
        /// <summary>Invisible character that Discord will accept where pure whitespace is otherwise not allowed.</summary>
        public const string Empty = "ᅠ";


        /// <summary>Returns the starting position of a command in a message, given a prefix.
        /// A space after the prefix is permitted. null is returned if the prefix isn't present.</summary>
        public static int? GetCommandPos(this DiscordMessage message, string prefix, StringComparison comparisonType = StringComparison.Ordinal)
        {
            var text = message.Content;
            if (string.IsNullOrEmpty(text) || text.Length < prefix.Length + 1) return null;
            if (!text.StartsWith(prefix, comparisonType)) return null;
            return text[prefix.Length] == ' ' ? prefix.Length + 1 : prefix.Length;
        }


        /// <summary>Returns the starting position of a command in a message, given the bot's mention as a prefix.
        /// A space after the prefix is permitted. null is returned if the mention isn't present.</summary>
        public static int? GetMentionCommandPos(this DiscordMessage message, InputService client)
        {
            var text = message.Content;
            if (string.IsNullOrEmpty(text) || !client.MentionPrefix.IsMatch(text)) return null;

            int pos = text.IndexOf('>') + 1;
            if (text.Length <= pos) return null;
            return text[pos] == ' ' ? pos + 1 : pos;
        }


        /// <summary>Adds a zero-width space after every "@" sign to prevent Discord mentions from firing.</summary>
        public static string SanitizeMentions(this string text)
        {
            return text.Replace("@", "@​");
        }


        /// <summary>Escapes markdown special characters for the purposes of Discord messages.</summary>
        public static string SanitizeMarkdown(this string text)
        {
            return Regex.Replace(text, @"([\\*_`~|])", "\\$1");
        }


        /// <summary>Extracts the contents of a code block if one is found, otherwise returns the same string trimmed.</summary>
        public static string ExtractCode(this string text)
        {
            var match = Regex.Match(text, "```[A-Za-z]{0,6}\n([\\s\\S]*)```");
            return match.Success ? match.Groups[1].Value : text.Trim().Trim('`');
        }


        /// <summary>Attempts to parse a unicode or guild emoji from its mention</summary>
        public static DiscordEmoji ToEmoji(this string text)
        {
            var match = Regex.Match(text.Trim(), @"^<?a?:?([a-zA-Z0-9_]+:[0-9]+)>?$");
            return DiscordEmoji.FromUnicode(match.Success ? match.Groups[1].Value : text.Trim());
        }


        /// <summary>Tries to convert a hexadecimal code or X11 color name into a <see cref="DiscordColor"/>.</summary>
        public static DiscordColor? ToColor(this string text)
        {
            text = text.ToLowerInvariant().Trim().Replace(" ", "");
            if (Regex.IsMatch(text, @"^[0123456789abcdef]{6}$")) text = $"#{text}";

            try
            {
                var color = (Color)new ColorConverter().ConvertFromString(text);
                return new DiscordColor(color.R, color.G, color.B);
            }
            catch // It's always System.Exception, and the inner exception can vary a lot, so screw it
            {
                return null;
            }
        }
    }
}
