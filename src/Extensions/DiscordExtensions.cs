using System.Linq;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using PacManBot.Constants;

namespace PacManBot.Extensions
{
    public static class DiscordExtensions
    {
        public const ChannelPermission CorrectDmPermissions = (ChannelPermission)37080128;



        // Discord objects


        /// <summary>Whether all the shards in a <see cref="DiscordShardedClient"/> are connected.</summary>
        public static bool AllShardsConnected(this DiscordShardedClient client)
        {
            return client.Shards.All(shard => shard.ConnectionState == ConnectionState.Connected);
        }


        /// <summary>Retrieves a <see cref="ISocketMessageChannel"/> from the given client.
        /// The value will be null if the channel doesn't exist, is not accesible, or isn't a message channel.</summary>
        public static ISocketMessageChannel GetMessageChannel(this BaseSocketClient client, ulong id)
        {
            return client.GetChannel(id) as ISocketMessageChannel;
        }


        /// <summary>Retrieves a user from the given context's guild that best satisfies the given string.
        /// The string can be a user ID, username or nickname.</summary>
        public static async Task<SocketGuildUser> ParseUserAsync(this ICommandContext context, string value)
        {
            var result = await new UserTypeReader<SocketGuildUser>().ReadAsync(context, value, null);
            return result.IsSuccess ? (SocketGuildUser)result.BestMatch : null;
        }


        /// <summary>Retrieves a <see cref="IUserMessage"/> from the given channel.
        /// The value will be null if the message doesn't exist, is not accesible, or isn't a user message.</summary>
        public static async Task<IUserMessage> GetUserMessageAsync(this ISocketMessageChannel channel, ulong id)
        {
            return await channel.GetMessageAsync(id, options: Bot.DefaultOptions) as IUserMessage;
        }


        /// <summary>Attempts to react to a given message using custom cross and check emojis depending on the condition.</summary>
        public static async Task AutoReactAsync(this IUserMessage message, bool success = true)
        {
            await message.AddReactionAsync(success ? CustomEmoji.ECheck : CustomEmoji.ECross, Bot.DefaultOptions);
        }


        /// <summary>Returns the mention of a channel to send in chat.</summary>
        public static string Mention(this IChannel channel)
        {
            return $"<#{channel.Id}>";
        }


        /// <summary>Returns the name of a channel, including its guild if it is a <see cref="IGuildChannel"/>.</summary>
        public static string NameAndGuild(this IChannel channel)
        {
            return $"{(channel is IGuildChannel gchannel ? $"{gchannel.Guild.Name}/" : "")}{channel.Name}";
        }


        /// <summary>Returns the guild (if applicable), name, and ID of a channel.</summary>
        public static string FullName(this IChannel channel)
        {
            return $"{channel.NameAndGuild()} ({channel.Id})";
        }


        /// <summary>Returns the name and discriminator of a user.</summary>
        public static string NameandDisc(this IUser user)
        {
            return $"{user.Username}#{user.Discriminator}";
        }


        /// <summary>Returns the name, discriminator and ID of a user.</summary>
        public static string FullName(this IUser user)
        {
            return $"{user.NameandDisc()} ({user.Id})";
        }




        // Permissions


        /// <summary>Whether the bot has the permission to perform an action in the given chanel.</summary>
        public static bool BotCan(this IChannel channel, ChannelPermission permission)
        {
            var perms = channel is IGuildChannel gchannel
                ? (ChannelPermission)gchannel.Guild.GetCurrentUserAsync().Result.GetPermissions(gchannel).RawValue
                : CorrectDmPermissions;

            return perms.HasFlag(permission);
        }


        /// <summary>Whether the bot has the permission to perform an action given the command context.</summary>
        public static bool BotCan(this SocketCommandContext context, ChannelPermission permission)
        {
            var perms = context.Guild != null
                ? (ChannelPermission)context.Guild.CurrentUser.GetPermissions(context.Channel as IGuildChannel).RawValue
                : CorrectDmPermissions;

            return perms.HasFlag(permission);
        }


        /// <summary>Whether the user who called this context's command has the permission to perform an action.</summary>
        public static bool UserCan(this SocketCommandContext context, ChannelPermission permission)
        {
            var perms = context.Guild != null
                ? (ChannelPermission)context.Guild.GetUser(context.User.Id).GetPermissions(context.Channel as IGuildChannel).RawValue
                : CorrectDmPermissions;

            return perms.HasFlag(permission);
        }




        // String utilities


        /// <summary>Adds a zero-width space after every "@" sign to prevent Discord mentions from firing.</summary>
        public static string SanitizeMentions(this string text)
        {
            return text.Replace("@", "@​");
        }


        /// <summary>Escapes markdown special characters for the purposes of Discord messages.</summary>
        public static string SanitizeMarkdown(this string text)
        {
            return Regex.Replace(text, @"([\\*_`~])", "\\$1");
        }


        /// <summary>Extracts the contents of a code block if one is found, otherwise returns the same string trimmed.</summary>
        public static string ExtractCode(this string text)
        {
            var match = Regex.Match(text, "```[A-Za-z]{0,6}\n([\\s\\S]*)```");
            return match.Success ? match.Groups[1].Value : text.Trim().Trim('`');
        }


        /// <summary>Attempts to parse a custom emoji from a string. Value will be null if it fails.</summary>
        public static Emote ToEmote(this string text)
        {
            return Emote.TryParse(text, out var emote) ? emote : null;
        }


        /// <summary>Converts unicode text into an <see cref="Emoji"/> object.
        /// Succeeds even if the text is not a valid emoji.</summary>
        public static Emoji ToEmoji(this string unicode)
        {
            return new Emoji(unicode);
        }
    }
}