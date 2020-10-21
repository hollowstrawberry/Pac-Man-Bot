using System;
using System.Threading.Tasks;
using DSharpPlus.Entities;

namespace PacManBot.Games
{
    /// <summary>
    /// The interface all games derive from.
    /// </summary>
    public interface IBaseGame
    {
        /// <summary>The displayed name of this game.</summary>
        string GameName { get; }

        /// <summary>Time after which a game will be routinely deleted due to inactivity.</summary>
        TimeSpan Expiry { get; }



        /// <summary>The state indicating whether a game is ongoing, or its ending reason.</summary>
        GameState State { get; set; }

        /// <summary>Date when this game was last accessed by a player.</summary>
        DateTime LastPlayed { get; set; }

        /// <summary>Individual actions taken since the game was created. It can mean different things for different games.</summary>
        int Time { get; set; }

        /// <summary>Discord snowflake ID of all users participating in this game.</summary>
        ulong[] UserId { get; set; } // Players



        /// <summary>Creates an updated string content for this game, to be put in a message.</summary>
        ValueTask<string> GetContentAsync(bool showHelp = true);

        /// <summary>Creates an updated message embed for this game.</summary>
        ValueTask<DiscordEmbedBuilder> GetEmbedAsync(bool showHelp = true);
    }
}
