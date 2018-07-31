using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

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


        /// <summary>Retrieves the channel where this game is taking place in.</summary>
        ISocketMessageChannel Channel { get; }

        /// <summary>Retrieves this game's channel's guild. Null when the channel is a DM channel.</summary>
        IGuild Guild { get; }

        /// <summary>Retrieves this game's latest message.</summary>
        Task<IUserMessage> GetMessage();
    }
}
