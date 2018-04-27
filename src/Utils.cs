using System;
using System.Text.RegularExpressions;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using static PacManBot.Modules.PacMan.GameInstance;

namespace PacManBot
{
    public static class Utils
    {
        // General utilities

        public enum TimePeriod
        {
            all = -1,
            month = 24 * 30,
            week = 24 * 7,
            day = 24,
            a = all, m = month, w = week, d = day //To be parsed from a string
        }

        public static string ScorePeriodString(TimePeriod period)
        {
            if (period == TimePeriod.month) return "in the last 30 days";
            else if (period == TimePeriod.week) return "in the last 7 days";
            else if (period == TimePeriod.day) return "in the last 24 hours";
            else return "of all time";
        }

        public static T Last<T>(this T[] array) => array[array.Length - 1];

        public static int LengthX<T>(this T[,] array) => array.GetLength(0);
        public static int LengthY<T>(this T[,] array) => array.GetLength(1);



        // Strings

        public static string Truncate(this string text, int maxLength)
        {
            return text.Substring(0, Math.Min(maxLength, text.Length));
        }

        public static string[] Split(this string text, string separator) //Shorthand
        {
            return text.Split(new string[] { separator }, StringSplitOptions.None);
        }

        public static bool ContainsAny(this string text, params string[] matches)
        {
            foreach(string match in matches)
            {
                if (text.Contains(match)) return true;
            }
            return false;
        }
        public static bool ContainsAny(this string text, params char[] matches)
        {
            foreach (char match in matches)
            {
                if (text.Contains(match.ToString())) return true;
            }
            return false;
        }

        //Conditional strings to help with complex text concatenation
        public static string If(this string text, bool condition) => condition ? text : "";
        public static string Unless(this string text, bool condition) => condition ? "" : text;



        // Discord utilities

        public static string SanitizeMentions(this string text)
        {
            return text.Replace("@", "@​"); // Zero-width space
        }

        public static string SanitizeMarkdown(this string text)
        {
            return Regex.Replace(text, @"([\\*_`~])", "\\$1");
        }


        public static bool BotHas(this IChannel channel, ChannelPermission permission)
        {
            return channel is IGuildChannel gchannel && gchannel.Guild != null && gchannel.Guild.GetCurrentUserAsync().Result.GetPermissions(gchannel).Has(permission);
        }

        public static bool BotHas(this SocketCommandContext context, ChannelPermission permission)
        {
            return context.Guild != null && context.Guild.CurrentUser.GetPermissions(context.Channel as IGuildChannel).Has(permission);
        }

        public static bool UserHas(this SocketCommandContext context, ChannelPermission permission)
        {
            return context.Guild != null && context.Guild.GetUser(context.User.Id).GetPermissions(context.Channel as IGuildChannel).Has(permission);
        }


        public static string FullChannelName(this SocketCommandContext context)
        {
            return $"{(context.Guild != null ? $"{context.Guild.Name}/" : "")}{context.Channel.Name} ({context.Channel.Id})";
        }

        public static string FullName(this SocketUser user)
        {
            return $"{user.Username}#{user.Discriminator}";
        }


        public static Emote ToEmote(this string text)
        {
            if (!Emote.TryParse(text, out Emote emote)) return null;
            return emote;
        }

        public static Emoji ToEmoji(this string unicode)
        {
            return new Emoji(unicode);
        }



        // Game utilities

        public static Dir Opposite(this Dir dir)
        {
            switch (dir)
            {
                case Dir.up:    return Dir.down;
                case Dir.down:  return Dir.up;
                case Dir.left:  return Dir.right;
                case Dir.right: return Dir.left;
                default: return Dir.none;
            }
        }

        public static Pos OfLength(this Dir dir, int num) //Converts a direction into what's essentially a vector
        {
            if (num < 0) num = 0;
            Pos pos = new Pos(0, 0);
            for (int i = 0; i < num; i++) pos += dir;
            return pos;
        }
    }
}
