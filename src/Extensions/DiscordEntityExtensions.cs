using System.Threading.Tasks;
using DSharpPlus.Entities;
using PacManBot.Constants;
using PacManBot.Games;

namespace PacManBot.Extensions
{
    public static class DiscordEntityExtensions
    {
        /// <summary>Attempts to react to a given message using custom cross and check emojis depending on the condition.</summary>
        public static async Task AutoReactAsync(this DiscordMessage message, bool success = true)
        {
            await message.CreateReactionAsync(success ? CustomEmoji.ECheck : CustomEmoji.ECross);
        }


        /// <summary>Modifies a message to update the game being displayed.</summary>
        public static async Task ModifyWithGameAsync(this DiscordMessage message, IChannelGame game)
        {
            await message.ModifyAsync(await game.GetContentAsync(), (await game.GetEmbedAsync()).Build());
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
    }
}