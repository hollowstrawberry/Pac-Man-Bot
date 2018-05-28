using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;

namespace PacManBot
{
    public static class Utils
    {
        public static readonly RequestOptions DefaultOptions = new RequestOptions()
        {
            RetryMode = RetryMode.RetryRatelimit,
            Timeout = 10000
        };

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



        // Lists

        public static T Get<T>(this IServiceProvider provider) // I thought the long name was ugly
        {
            return provider.GetRequiredService<T>();
        }


        public static T Last<T>(this IList<T> list)
        {
            return list[list.Count - 1];
        }


        public static IEnumerable<IEnumerable<T>> Permutations<T>(this IEnumerable<T> list, int length)
        {
            if (length == 1) return list.Select(t => new T[] { t });
            return Permutations(list, length - 1).SelectMany(t => list, (t1, t2) => t1.Concat(new T[] { t2 }));
        }



        // Random

        public static T Choose<T>(this Random random, T[] values)
        {
            return values[random.Next(values.Length)];
        }

        public static T Choose<T>(this Random random, IList<T> values)
        {
            return values[random.Next(values.Count)];
        }

        public static double NextDouble(this Random random, double min, double max)
        {
            return random.NextDouble() * (max - min) + min;
        }



        // Misc

        public static int Ceiling(this double num)
        {
            return (int)Math.Ceiling(num);
        }


        public static string Humanized(this TimeSpan span)
        {
            int days = (int)span.TotalDays, hours = span.Hours, minutes = span.Minutes;
            string result = $"{days} day{"s".If(days > 1)}, ".If(days > 0) + $"{hours} hour{"s".If(hours > 1)}, ".If(hours > 0) + $"{minutes} minute{"s".If(minutes > 1)}".If(minutes > 0);
            return result != "" ? result : "Just now";
        }




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


        public static bool ContainsAny(this string text, params string[] values)
        {
            foreach(string value in values)
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


        public static bool IsImageUrl(string URL)
        {
            try
            {
                var req = WebRequest.Create(URL);
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




        /* ===== Discord ===== */


        public static bool AllShardsConnected(this DiscordShardedClient client)
        {
            foreach (var shard in client.Shards)
            {
                if (shard.ConnectionState != ConnectionState.Connected) return false;
            }
            return true;
        }


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


        // Emotes

        public static Emote ToEmote(this string text)
        {
            if (!Emote.TryParse(text, out Emote emote)) return null;
            return emote;
        }


        public static Emoji ToEmoji(this string unicode)
        {
            return new Emoji(unicode);
        }
    }
}
