using System;
using System.Text.RegularExpressions;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using static PacManBot.Modules.PacMan.PacManGame;

namespace PacManBot
{
    public static class Utils
    {
        //General utilities

        //Conditional strings to help with complex text concatenation
        public static string If(this string text, bool condition) => condition ? text : "";
        public static string Unless(this string text, bool condition) => condition ? "" : text;

        //2-dimensional array length
        public static int LengthX<T>(this T[,] array) => array.GetLength(0);
        public static int LengthY<T>(this T[,] array) => array.GetLength(1);

        //Shorthand
        public static string[] Split(this string text, string separator)
        {
            return text.Split(new string[] { separator }, StringSplitOptions.None);
        }


        //Discord utilities

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

        public static bool CheckHasEmbedPermission(this SocketCommandContext context)
        {
            if (context.Guild != null && !context.BotHas(ChannelPermission.EmbedLinks))
            {
                context.Channel.SendMessageAsync("To show a fancy new information block, this bot requires the permission to Embed Links!");
                return false;
            }
            return true;
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

        public static Pos OfLength(this Dir dir, int num) //Converts a direction into a position vector
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
            if (!Regex.IsMatch(key, @"^\{.*\}$")) key = $"{{{key}}}"; //Adds curly brackets

            int keyIndex = text.IndexOf(key); //Key start location
            int valIndex = keyIndex + key.Length; //Value start location

            int endIndex; //Value end location
            if (Regex.IsMatch(text, @"(" + Regex.Escape(key) + @"[\S\s]*){2}")) //More than one key instance: multiline
            {
                endIndex = text.IndexOf(key, valIndex);
            }
            else endIndex = text.IndexOf("\n", valIndex);
            if (endIndex < 0) endIndex = text.Length;

            string stringValue = keyIndex < 0 ? null : text.Substring(valIndex, endIndex - valIndex).Trim('\n', '\r');

            try
            {
                return (T)Convert.ChangeType(stringValue, typeof(T));
            }
            catch (InvalidCastException)
            {
                return default(T);
            }
        }
    }
}