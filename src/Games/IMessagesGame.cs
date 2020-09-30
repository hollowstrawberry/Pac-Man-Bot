using System.Threading.Tasks;

namespace PacManBot.Games
{
    /// <summary>
    /// The interface for <see cref="IChannelGame"/>s that receive message input.
    /// </summary>
    public interface IMessagesGame : IChannelGame
    {
        /// <summary>Whether the given value is a valid input given the player sending it.</summary>
        ValueTask<bool> IsInputAsync(string value, ulong userId);

        /// <summary>Executes an input expected to be valid.</summary>
        Task InputAsync(string input, ulong userId = 1);
    }
}
