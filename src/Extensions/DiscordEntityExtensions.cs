using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.Entities;
using PacManBot.Constants;
using PacManBot.Services;

namespace PacManBot.Extensions
{
    public static class DiscordEntityExtensions
    {
        /// <summary>Discord permissions in a DM channel.</summary>
        public const Permissions DmPermissions = (Permissions)37080128;

        private static readonly Regex YesNoRegex = new Regex(@"^(ye?s?|no?)$", RegexOptions.IgnoreCase);

        /// <summary>Sends a message in the current context</summary>
        public static Task<DiscordMessage> RespondAsync(this CommandContext ctx, object message, DiscordEmbedBuilder embed = null)
            => ctx.RespondAsync(message?.ToString(), false, embed?.Build());

        /// <summary>Sends a message in the current context containing only an embed</summary>
        public static Task<DiscordMessage> RespondAsync(this CommandContext ctx, DiscordEmbedBuilder embed)
            => ctx.RespondAsync(null, false, embed.Build());


        /// <summary>Returns whether the next message by the user in this context is equivalent to "yes".</summary>
        public static async Task<bool?> GetYesResponseAsync(this CommandContext ctx, int timeout = 30)
        {
            var response = await ctx.GetResponseAsync(x => YesNoRegex.IsMatch(x.Content), timeout);
            return response?.Content.StartsWith("y", StringComparison.OrdinalIgnoreCase);
        }


        /// <summary>Returns the first new message from the user in this context,
        /// or null if no message is received within the timeout in seconds.</summary>
        public static async Task<DiscordMessage> GetResponseAsync(this CommandContext ctx, int timeout = 30)
        {
            return await ctx.Services.Get<InputService>().GetResponseAsync(
                x => x.Channel.Id == ctx.Channel.Id && x.Author.Id == ctx.User.Id, timeout);
        }

        /// <summary>Returns the first new message from the user in this context that satisfies additional conditions.
        /// The value will be null if no valid response is received within the timeout in seconds.</summary>
        public static async Task<DiscordMessage> GetResponseAsync(this CommandContext ctx, Func<DiscordMessage, bool> extraConditions, int timeout = 30)
        {
            return await ctx.Services.Get<InputService>().GetResponseAsync(
                x => x.Channel.Id == ctx.Channel.Id && x.Author.Id == ctx.User.Id && extraConditions(x), timeout);
        }




        /// <summary>Whether the bot has the permission to perform an action in the given chanel.</summary>
        public static bool BotCan(this DiscordChannel channel, Permissions permission)
        {
            return channel.Guild is null
                ? DmPermissions.HasFlag(permission)
                : channel.PermissionsFor(channel.Guild.CurrentMember).HasFlag(permission);
        }

        /// <summary>Whether the bot has the permission to perform an action given the command context.</summary>
        public static bool BotCan(this CommandContext ctx, Permissions permission)
        {
            return ctx.Guild is null
                ? DmPermissions.HasFlag(permission)
                : ctx.Channel.PermissionsFor(ctx.Guild.CurrentMember).HasFlag(permission);
        }

        /// <summary>Whether the user who called this context's command has the permission to perform an action.</summary>
        public static bool UserCan(this CommandContext ctx, Permissions permission)
        {
            return ctx.Guild is null
                ? DmPermissions.HasFlag(permission)
                : ctx.Channel.PermissionsFor(ctx.Member).HasFlag(permission);
        }


        /// <summary>Attempts to react to a given message using custom cross and check emojis depending on the condition.</summary>
        public static Task AutoReactAsync(this DiscordMessage message, bool success = true)
            => message.CreateReactionAsync(success ? CustomEmoji.ECheck : CustomEmoji.ECross);


        /// <summary>Reacts to the command's calling message with a check or cross.</summary>
        public static Task AutoReactAsync(this CommandContext ctx, bool success = true)
            => ctx.Message.AutoReactAsync(success);


        /// <summary>The nickname of this user if it has one, otherwise its username.</summary>
        public static string DisplayName(this DiscordUser user)
        {
            return user is DiscordMember member && !string.IsNullOrWhiteSpace(member.Nickname)
                ? member.Nickname
                : user.Username;
        }


        /// <summary>Returns the name of a channel, including its guild if it is a <see cref="IGuildChannel"/>.</summary>
        public static string NameAndGuild(this DiscordChannel channel)
            => $"{(channel.Guild is null ? "" : $"{channel.Guild.Name}/")}{channel.Name}";


        /// <summary>Returns the name and discriminator of a user.</summary>
        public static string NameandDisc(this DiscordUser user)
            => $"{user.Username}#{user.Discriminator}";


        /// <summary>Returns the name, discriminator and ID of a user.</summary>
        public static string DebugName(this DiscordUser user)
            => $"{user.NameandDisc()} ({user.Id})";

        /// <summary>Returns the guild (if applicable), name, and ID of a channel.</summary>
        public static string DebugName(this DiscordChannel channel)
            => $"{channel.NameAndGuild()} ({channel.Id})";

        /// <summary>Returns the name and ID of a guild.</summary>
        public static string DebugName(this DiscordGuild guild)
            => $"{guild.Name} ({guild.Id})";
    }
}