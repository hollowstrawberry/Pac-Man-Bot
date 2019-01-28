using System;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using PacManBot.Extensions;

namespace PacManBot.Games
{
    /// <summary>
    /// A type of game that takes place in a specific channel. Implements <see cref="IChannelGame"/>.
    /// </summary>
    public abstract class ChannelGame : BaseGame, IChannelGame
    {
        private ISocketMessageChannel internalChannel;
        private IUserMessage internalMessage;


        /// <summary>Discord snowflake ID of the channel where this game is taking place in.</summary>
        public virtual ulong ChannelId { get; set; }
        /// <summary>Discord snowflake ID of the latest message used by this game.</summary>
        public virtual ulong MessageId { get; set; }


        /// <summary>Retrieves this game's channel's guild. Null when the channel is a DM channel.</summary>
        public IGuild Guild => (Channel as IGuildChannel)?.Guild;

        /// <summary>Retrieves the channel where this game is taking place in.</summary>
        public ISocketMessageChannel Channel
        {
            get => internalChannel != null && internalChannel.Id == ChannelId
                ? internalChannel : (internalChannel = client.GetMessageChannel(ChannelId)); // Lazy load
        }

        /// <summary>Retrieves this game's latest message. Null if not retrievable.</summary>
        public async Task<IUserMessage> GetMessage()
        {
            if (MessageId == 0 || Channel == null) return (internalMessage = null);

            return internalMessage != null && internalMessage.Id == MessageId
                ? internalMessage : (internalMessage = await Channel.GetUserMessageAsync(MessageId)); // Lazy load
        }


        /// <summary>Empty game constructor, used only with reflection and serialization.</summary>
        protected ChannelGame() { }

        /// <summary>Creates a new game instance in the specified channel and with the specified players.</summary>
        protected ChannelGame(ulong channelId, ulong[] userIds, IServiceProvider services)
            : base(userIds, services)
        {
            ChannelId = channelId;
        }


        /// <summary>Used to remove the guild prefix from game input, as it is to be ignored.</summary>
        protected string StripPrefix(string value)
        {
            string prefix = storage.GetPrefix(Channel);
            return value.StartsWith(prefix) ? value.Substring(prefix.Length) : value;
        }
    }
}
