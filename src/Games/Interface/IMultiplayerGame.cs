using DSharpPlus.Entities;
using System.Threading.Tasks;

namespace PacManBot.Games
{
    /// <summary>
    /// The interface for multiplayer games, giving access to player and AI members.
    /// </summary>
    public interface IMultiplayerGame : IBaseGame
    {
        /// <summary>The current <see cref="Player"/> whose turn it is.</summary>
        Player Turn { get; }

        /// <summary>This game's winner. <see cref="Player.None"/> if none.</summary>
        Player Winner { get; }

        /// <summary>The message displayed at the top of this game.</summary>
        string Message { get; }

        /// <summary>Whether the current turn belongs to a bot.</summary>
        ValueTask<bool> IsBotTurnAsync();

        /// <summary>Retrieves the user at the specified index. Null if unreachable or not found.</summary>
        ValueTask<DiscordUser> GetUserAsync(int i = 0);

        /// <summary>Executes automatic AI input, assuming it is a bot's turn.</summary>
        Task BotInputAsync();
    }
}
