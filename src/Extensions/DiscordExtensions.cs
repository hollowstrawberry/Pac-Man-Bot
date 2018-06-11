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


        // Misc

        public static bool AllShardsConnected(this DiscordShardedClient client)
        {
            foreach (var shard in client.Shards)
            {
                if (shard.ConnectionState != ConnectionState.Connected) return false;
            }
            return true;
        }


        public static async Task<SocketGuildUser> ParseUser(this ICommandContext context, string value)
        {
            var result = await new UserTypeReader<SocketGuildUser>().ReadAsync(context, value, null);
            if (result.IsSuccess) return (SocketGuildUser)result.BestMatch;
            else return null;
        }


        public static string SanitizeMentions(this string text)
        {
            return text.Replace("@", "@​"); // Zero-width space
        }


        public static string SanitizeMarkdown(this string text)
        {
            return Regex.Replace(text, @"([\\*_`~])", "\\$1"); // Backslash in front
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