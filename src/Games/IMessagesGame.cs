
namespace PacManBot.Games
{
    /// <summary>
    /// The interface for <see cref="IChannelGame"/>s that receive message input.
    /// </summary>
    public interface IMessagesGame : IChannelGame
    {
        /// <summary>Whether the given value is a valid input given the player sending it.</summary>
        bool IsInput(string value, ulong userId);

        /// <summary>Executes an input expected to be valid, specifying the player sending it if necessary.</summary>
        void Input(string input, ulong userId = 1);
    }
}
