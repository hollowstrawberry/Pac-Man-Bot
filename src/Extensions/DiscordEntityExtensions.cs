using System;
using System.Linq;
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
            => ctx.RespondAsync(message?.ToString(), embed?.Build());

        /// <summary>Sends a message in the current context containing only an embed</summary>
        public static Task<DiscordMessage> RespondAsync(this CommandContext ctx, DiscordEmbedBuilder embed)
            => ctx.RespondAsync(null, embed.Build());

        /// <summary>Sends a reply to this message.</summary>
        public static Task<DiscordMessage> ReplyAsync(this DiscordMessage msg, string content, bool ping = true)
            => msg.Channel.SendMessageAsync(new DiscordMessageBuilder().WithContent(content).WithReply(msg.Id, ping));

        /// <summary>Sends a reply to this message.</summary>
        public static Task<DiscordMessage> ReplyAsync(this DiscordMessage msg, DiscordEmbedBuilder embed, bool ping = true)
            => msg.Channel.SendMessageAsync(new DiscordMessageBuilder().WithEmbed(embed.Build()).WithReply(msg.Id, ping));

        /// <summary>Sends a reply to this context's message.</summary>
        public static Task<DiscordMessage> ReplyAsync(this CommandContext ctx, string content, bool ping = true)
            => ctx.Message.ReplyAsync(content, ping);

        /// <summary>Sends a reply to this context's message.</summary>
        public static Task<DiscordMessage> ReplyAsync(this CommandContext ctx, DiscordEmbedBuilder embed, bool ping = true)
            => ctx.Message.ReplyAsync(embed, ping);

        /// <summary>Creates a reaction to this message from an emoji mention.</summary>
        public static Task CreateReactionAsync(this DiscordMessage message, string emoji)
            => message.CreateReactionAsync(emoji.ToEmoji());

        /// <summary>Deletes your own reaction.</summary>
        public static Task DeleteOwnReactionAsync(this DiscordMessage message, string emoji)
            => message.DeleteOwnReactionAsync(emoji.ToEmoji());

        /// <summary>Deletes all reactions of a specific reaction to this message.</summary>
        public static Task DeleteReactionsEmojiAsync(this DiscordMessage message, string emoji)
            => message.DeleteReactionsEmojiAsync(emoji.ToEmoji());

        /// <summary>Deletes another user's reaction.</summary>
        public static Task DeleteReactionAsync(this DiscordMessage message, string emoji, DiscordUser user, string reason = null)
            => message.DeleteReactionAsync(emoji.ToEmoji(), user, reason);




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

        /// <summary>Attempts to react to a given message using custom cross and check emojis depending on the condition.</summary>
        public static Task AutoReactAsync(this DiscordMessage message, bool success = true)
            => message.CreateReactionAsync(success ? CustomEmoji.Check : CustomEmoji.Cross);

        /// <summary>Reacts to the command's calling message with a check or cross.</summary>
        public static Task AutoReactAsync(this CommandContext ctx, bool success = true)
            => ctx.Message.AutoReactAsync(success);




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




        /// <summary>Grabs a guild object in cache from an ID.</summary>
        public static DiscordGuild GetGuild(this DiscordShardedClient client, ulong id)
        {
            foreach (var shard in client.ShardClients.Values)
            {
                if (shard.Guilds.TryGetValue(id, out var guild)) return guild;
            }
            return null;
        }

        /// <summary>Grabs a channel object in cache from an ID.</summary>
        public static DiscordChannel GetChannel(this DiscordShardedClient client, ulong id, InputService inp = null)
        {
            if (inp is not null && inp.GetDmChannel(id) is DiscordChannel ch) return ch;

            foreach (var shard in client.ShardClients.Values)
            {
                if (shard.PrivateChannels.TryGetValue(id, out var channel)) return channel;
            }
            foreach (var guild in client.ShardClients.Values.SelectMany(x => x.Guilds.Values))
            {
                if (guild.Channels.TryGetValue(id, out var channel)) return channel;
            }
            return null;
        }

        /// <summary>Grabs a channel object in cache from an ID.</summary>
        public static DiscordChannel GetChannel(this DiscordClient shard, ulong id, InputService inp = null)
        {
            if (inp is not null && inp.GetDmChannel(id) is DiscordChannel ch) return ch;
            if (shard.PrivateChannels.TryGetValue(id, out var cha)) return cha;

            foreach (var guild in shard.Guilds.Values)
            {
                if (guild.Channels.TryGetValue(id, out var channel)) return channel;
            }
            return null;
        }

        /// <summary>Grabs a member object in cache from an ID.</summary>
        public static DiscordMember GetMember(this DiscordShardedClient client, ulong id)
        {
            foreach (var guild in client.ShardClients.Values.SelectMany(x => x.Guilds.Values))
            {
                if (guild.Members.TryGetValue(id, out var member)) return member;
            }
            return null;
        }

        /// <summary>Grabs a member object in cache from an ID.</summary>
        public static DiscordMember GetMember(this DiscordClient shard, ulong id)
        {
            foreach (var guild in shard.Guilds.Values)
            {
                if (guild.Members.TryGetValue(id, out var member)) return member;
            }
            return null;
        }
    }
}