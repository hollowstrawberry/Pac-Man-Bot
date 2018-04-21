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
        //General utilities

        //2-dimensional array length
        public static int LengthX<T>(this T[,] array) => array.GetLength(0);
        public static int LengthY<T>(this T[,] array) => array.GetLength(1);


        //Strings

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

        //Conditional strings to help with complex text concatenation
        public static string If(this string text, bool condition) => condition ? text : "";
        public static string Unless(this string text, bool condition) => condition ? "" : text;


        //Discord utilities

        public static string SanitizeMentions(this string text)
        {
            return text.Replace("@", "@​"); // Zero-width space
        }

        public static string SanitizeMarkdown(this string text)
        {
            return Regex.Replace(text, @"([\\*_`~])", "\\$1");
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
            return (context.Guild != null ? $"{context.Guild.Name}/" : "") + context.Channel.Name;
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


        //Game utilities

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


        //Bot utility

        public static string FindValue(this string text, string key)
        {
            return FindValue<string>(text, key);
        }
        public static T FindValue<T>(this string text, string key) where T : IConvertible
        {
            string value = null;

            if (key[0] != '{') key = $"{{{key}}}"; //Adds curly brackets

            int keyIndex = text.IndexOf(key); //Key start location
            if (keyIndex > -1)
            {
                int valIndex = keyIndex + key.Length; //Value start location

                int nextKeyIndex = text.IndexOf(key, valIndex);
                int endIndex = nextKeyIndex > -1 ? nextKeyIndex : text.IndexOf('\n', valIndex); // Stops at either a newline or the second instance of the key
                if (endIndex < 0) endIndex = text.Length;

                value = text.Substring(valIndex, endIndex - valIndex).Trim('\n');
            }

            try
            {
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch (InvalidCastException)
            {
                return default(T);
            }
        }
    }
}
