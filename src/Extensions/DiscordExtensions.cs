using System.Linq;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace PacManBot.Extensions
{
    public static class DiscordExtensions
    {
        public const ChannelPermission CorrectDmPermissions = (ChannelPermission)37080128;


        // Discord objects

        public static bool AllShardsConnected(this DiscordShardedClient client)
        {
            return client.Shards.All(shard => shard.ConnectionState == ConnectionState.Connected);
        }


        public static ISocketMessageChannel GetMessageChannel(this BaseSocketClient client, ulong id)
        {
            return client.GetChannel(id) as ISocketMessageChannel;
        }


        public static async Task<SocketGuildUser> ParseUserAsync(this ICommandContext context, string value)
        {
            var result = await new UserTypeReader<SocketGuildUser>().ReadAsync(context, value, null);
            return result.IsSuccess ? (SocketGuildUser)result.BestMatch : null;
        }


        public static async Task<IUserMessage> GetUserMessageAsync(this ISocketMessageChannel channel, ulong id)
        {
            return await channel.GetMessageAsync(id, options: Bot.DefaultOptions) as IUserMessage;
        }


        public static async Task AutoReactAsync(this IUserMessage message, bool success = true)
        {
            await message.AddReactionAsync(success ? CustomEmoji.ECheck : CustomEmoji.ECross, Bot.DefaultOptions);
        }


        public static string NameAndGuild(this IChannel channel)
        {
            return $"{(channel is IGuildChannel gchannel ? $"{gchannel.Guild.Name}/" : "")}{channel.Name}";
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




        // Permissions

        public static bool BotCan(this IChannel channel, ChannelPermission permission)
        {
            var perms = channel is IGuildChannel gchannel
                ? (ChannelPermission)gchannel.Guild.GetCurrentUserAsync().Result.GetPermissions(gchannel).RawValue
                : CorrectDmPermissions;

            return perms.HasFlag(permission);
        }

        public static bool BotCan(this SocketCommandContext context, ChannelPermission permission)
        {
            var perms = context.Guild != null
                ? (ChannelPermission)context.Guild.CurrentUser.GetPermissions(context.Channel as IGuildChannel).RawValue
                : CorrectDmPermissions;

            return perms.HasFlag(permission);
        }


        public static bool UserCan(this SocketCommandContext context, ChannelPermission permission)
        {
            var perms = context.Guild != null
                ? (ChannelPermission)context.Guild.GetUser(context.User.Id).GetPermissions(context.Channel as IGuildChannel).RawValue
                : CorrectDmPermissions;

            return perms.HasFlag(permission);
        }




        // String utilities

        public static string SanitizeMentions(this string text)
        {
            return text.Replace("@", "@​"); // Zero-width space
        }


        public static string SanitizeMarkdown(this string text)
        {
            return Regex.Replace(text, @"([\\*_`~])", "\\$1"); // Backslash in front
        }


        public static Emote ToEmote(this string text)
        {
            return Emote.TryParse(text, out var emote) ? emote : null;
        }


        public static Emoji ToEmoji(this string unicode)
        {
            return new Emoji(unicode);
        }
    }
}