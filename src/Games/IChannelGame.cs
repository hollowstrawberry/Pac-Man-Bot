using System;
using System.Threading.Tasks;
using DSharpPlus.Entities;

namespace PacManBot.Games
{
    /// <summary>
    /// The interface for channel-specific games, giving access to channel, guild and message members.
    /// </summary>
    public interface IChannelGame : IBaseGame
    {
        /// <summary>Discord snowflake ID of the channel where this game is taking place in.</summary>
        ulong ChannelId { get; set; }

        /// <summary>Discord snowflake ID of the latest message used by this game.</summary>
        ulong MessageId { get; set; }

        /// <summary>The time when the message used by this game was last moved.</summary>
        DateTime LastBumped { get; set; }

        /// <summary>Retrieves the channel where this game is taking place in.</summary>
        ValueTask<DiscordChannel> GetChannelAsync();

        /// <summary>Retrieves this game's latest message.</summary>
        ValueTask<DiscordMessage> GetMessageAsync();
    }
}
