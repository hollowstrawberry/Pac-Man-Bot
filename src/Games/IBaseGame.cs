using System;
using Discord;

namespace PacManBot.Games
{
    /// <summary>
    /// The interface all games derive from.
    /// </summary>
    public interface IBaseGame
    {
        /// <summary>The displayed name of this game.</summary>
        string Name { get; }

        /// <summary>Time after which a game will be routinely deleted due to inactivity.</summary>
        TimeSpan Expiry { get; }



        /// <summary>The state indicating whether a game is ongoing, or its ending reason.</summary>
        State State { get; set; }

        /// <summary>Date when this game was last accessed by a player.</summary>
        DateTime LastPlayed { get; set; }

        /// <summary>Individual actions taken since the game was created. It can mean different things for different games.</summary>
        int Time { get; set; }

        /// <summary>Discord snowflake ID of all users participating in this game.</summary>
        ulong[] UserId { get; set; } // Players



        /// <summary>Creates an updated string content for this game, to be put in a message.</summary>
        string GetContent(bool showHelp = true);

        /// <summary>Creates an updated message embed for this game.</summary>
        EmbedBuilder GetEmbed(bool showHelp = true);

        /// <summary>Creates a <see cref="RequestOptions"/> to be used in Discord tasks related to this game,
        /// such as message editions. Tasks with these options can be cancelled using <see cref="CancelRequests"/>.</summary>
        RequestOptions GetRequestOptions();

        /// <summary>Creates a delegate to be passed to <see cref="IUserMessage.ModifyAsync(Action{MessageProperties}, RequestOptions)"/>.
        /// The message will be updated with the latest content and embed from this game.</summary>
        Action<MessageProperties> GetMessageUpdate();

        /// <summary>Cancels all previous Discord requests made using the options from <see cref="GetRequestOptions"/>.</summary>
        void CancelRequests();
    }
}
