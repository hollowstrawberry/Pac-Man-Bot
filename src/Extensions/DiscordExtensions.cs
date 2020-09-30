using Serilog.Events;
using System;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.CommandsNext;
using PacManBot.Constants;
using PacManBot.Services;
using Microsoft.Extensions.Logging;

namespace PacManBot.Extensions
{
    public static class DiscordExtensions
    {
        /// <summary>Discord permissions in a DM channel.</summary>
        public const Permissions DmPermissions = (Permissions)37080128;

        /// <summary>Invisible character that Discord will accept where pure whitespace is otherwise not allowed.</summary>
        public const string Empty = "ᅠ";



        // Discord objects


        /// <summary>Attempts to react to a given message using custom cross and check emojis depending on the condition.</summary>
        public static async Task AutoReactAsync(this DiscordMessage message, bool success = true)
        {
            await message.CreateReactionAsync(success ? CustomEmoji.ECheck : CustomEmoji.ECross);
        }


        /// <summary>Returns the name of a channel, including its guild if it is a <see cref="IGuildChannel"/>.</summary>
        public static string NameAndGuild(this DiscordChannel channel)
        {
            return $"{(channel.Guild == null ? "" : $"{channel.Guild.Name}/")}{channel.Name}";
        }


        /// <summary>Returns the guild (if applicable), name, and ID of a channel.</summary>
        public static string DebugName(this DiscordChannel channel)
        {
            return $"{channel.NameAndGuild()} ({channel.Id})";
        }


        /// <summary>Returns the name and ID of a guild.</summary>
        public static string DebugName(this DiscordGuild guild)
        {
            return $"{guild.Name} ({guild.Id})";
        }


        /// <summary>The nickname of this user if it has one, otherwise its username.</summary>
        public static string DisplayName(this DiscordUser user)
        {
            return user is DiscordMember member && !string.IsNullOrWhiteSpace(member.Nickname)
                ? member.Nickname
                : user.Username;
        }


        /// <summary>Returns the name and discriminator of a user.</summary>
        public static string NameandDisc(this DiscordUser user)
        {
            return $"{user.Username}#{user.Discriminator}";
        }


        /// <summary>Returns the name, discriminator and ID of a user.</summary>
        public static string DebugName(this DiscordUser user)
        {
            return $"{user.NameandDisc()} ({user.Id})";
        }




        // Permissions

        /// <summary>Whether the bot has the permission to perform an action in the given chanel.</summary>
        public static bool BotCan(this DiscordChannel channel, Permissions permission)
        {
            if (channel.Guild == null) return DmPermissions.HasFlag(permission);
            return channel.PermissionsFor(channel.Guild.CurrentMember).HasFlag(permission);
        }


        /// <summary>Whether the bot has the permission to perform an action given the command context.</summary>
        public static bool BotCan(this CommandContext context, Permissions permission)
        {
            if (context.Guild == null) return DmPermissions.HasFlag(permission);
            return context.Channel.PermissionsFor(context.Guild.CurrentMember).HasFlag(permission);
        }


        /// <summary>Whether the user who called this context's command has the permission to perform an action.</summary>
        public static bool UserCan(this CommandContext context, Permissions permission)
        {
            if (context.Guild == null) return DmPermissions.HasFlag(permission);
            return context.Channel.PermissionsFor(context.Member).HasFlag(permission);
        }




        // String utilities


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