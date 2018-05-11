using System;
using System.Text;
using System.Text.RegularExpressions;
using Discord;
using Discord.Commands;
using Microsoft.Extensions.DependencyInjection;
using static PacManBot.Modules.PacMan.GameInstance;

namespace PacManBot
{
    public static class Utils
    {
        public static readonly RequestOptions DefaultRequestOptions = new RequestOptions() { RetryMode = RetryMode.RetryRatelimit, Timeout = 10000 };
        public const ChannelPermission CorrectDmPermissions = (ChannelPermission)37080128;


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
            switch (period)
            {
                case TimePeriod.month: return "in the last 30 days";
                case TimePeriod.week: return "in the last 7 days";
                case TimePeriod.day: return "in the last 24 hours";
                default: return "of all time";
            }
        }


        public static T Get<T>(this IServiceProvider provider) // I thought the long name was ugly
        {
            return provider.GetRequiredService<T>();
        }



        // Arrays

        public static T Last<T>(this T[] array) => array[array.Length - 1];

        public static int LengthX<T>(this T[,] array) => array.GetLength(0);
        public static int LengthY<T>(this T[,] array) => array.GetLength(1);




        /* ===== Strings ===== */


        public static string If(this string text, bool condition) => condition ? text : ""; //Helps with complex text concatenation


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




        /* ===== Discord ===== */


        public static string SanitizeMentions(this string text)
        {
            return text.Replace("@", "@​"); // Zero-width space
        }

        
        public static string SanitizeMarkdown(this string text)
        {
            return Regex.Replace(text, @"([\\*_`~])", "\\$1");
        }


        // Permissions

        public static bool BotCan(this IChannel channel, ChannelPermission permission)
        {
            ChannelPermission perms = channel is IGuildChannel gchannel ? (ChannelPermission)gchannel.Guild.GetCurrentUserAsync().Result.GetPermissions(gchannel).RawValue : CorrectDmPermissions;
            return perms.HasFlag(permission);
        }

        public static bool BotCan(this SocketCommandContext context, ChannelPermission permission)
        {
            ChannelPermission perms = context.Guild == null ? CorrectDmPermissions : (ChannelPermission)context.Guild.CurrentUser.GetPermissions(context.Channel as IGuildChannel).RawValue;
            return perms.HasFlag(permission);
        }


        public static bool UserCan(this SocketCommandContext context, ChannelPermission permission)
        {
            ChannelPermission perms = context.Guild == null ? CorrectDmPermissions : (ChannelPermission)context.Guild.GetUser(context.User.Id).GetPermissions(context.Channel as IGuildChannel).RawValue;
            return perms.HasFlag(permission);
        }


        // Names

        public static string NameAndGuild(this IChannel channel)
        {
            return $"{((channel is IGuildChannel gchannel) ? $"{gchannel.Guild.Name}/" : "")}{channel.Name}";
        }


        public static string FullName(this IChannel channel)
        {
            return $"{channel.NameAndGuild()} ({channel.Id})";
        }


        public static string NameandNum(this IUser user)
        {
            return $"{user.Username}#{user.Discriminator}";
        }


        public static string FullName(this IUser user)
        {
            return $"{user.NameandNum()} ({user.Id})";
        }


        // Etc

        public static Emote ToEmote(this string text)
        {
            if (!Emote.TryParse(text, out Emote emote)) return null;
            return emote;
        }


        public static Emoji ToEmoji(this string unicode)
        {
            return new Emoji(unicode);
        }




        /* ===== Pac-Man ===== */

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
