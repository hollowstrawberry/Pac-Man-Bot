using Discord;

namespace PacManBot.Games
{
    /// <summary>
    /// The interface for user-specific games.
    /// Has prevalence over <see cref="IChannelGame"/> when sorting between the two.
    /// </summary>
    public interface IUserGame : IBaseGame
    {
        /// <summary>Discord snowflake ID of the user whose game this is.</summary>
        ulong OwnerId { get; }

        /// <summary>Retrieves the user whose game this is.</summary>
        IUser Owner { get; }
    }
}
