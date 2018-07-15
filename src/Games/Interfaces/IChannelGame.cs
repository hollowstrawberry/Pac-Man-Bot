using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace PacManBot.Games
{
    public interface IChannelGame : IBaseGame
    {
        /// <summary>Discord snowflake ID of the channel where this game is taking place in.</summary>
        ulong ChannelId { get; set; }

        /// <summary>Discord snowflake ID of the latest message used by this game.</summary>
        ulong MessageId { get; set; }


        /// <summary>Retrieves the channel where this game is taking place in.</summary>
        ISocketMessageChannel Channel { get; }

        /// <summary>Retrieves this game's channel's guild. Null when the channel is a DM channel.</summary>
        SocketGuild Guild { get; }

        /// <summary>Retrieves this game's latest message.</summary>
        Task<IUserMessage> GetMessage();
    }


    public interface IMessagesGame : IChannelGame
    {
        /// <summary>Whether the given value is a valid input given the player sending it.</summary>
        bool IsInput(string value, ulong userId);

        /// <summary>Executes an input expected to be valid, specifying the player sending it if necessary.</summary>
        void Input(string input, ulong userId = 1);
    }


    public interface IReactionsGame : IChannelGame
    {
        /// <summary>Whether the given value is a valid input given the player sending it.</summary>
        bool IsInput(IEmote value, ulong userId);

        /// <summary>Executes an input expected to be valid, specifying the player sending it if necessary.</summary>
        void Input(IEmote input, ulong userId = 1);
    }
}
