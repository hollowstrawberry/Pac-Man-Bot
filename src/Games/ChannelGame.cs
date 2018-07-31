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
        private ISocketMessageChannel _channel;
        private IUserMessage _message;


        /// <summary>Discord snowflake ID of the channel where this game is taking place in.</summary>
        public virtual ulong ChannelId { get; set; }

        /// <summary>Discord snowflake ID of the latest message used by this game.</summary>
        public virtual ulong MessageId { get; set; }


        /// <summary>Retrieves the channel where this game is taking place in.</summary>
        public ISocketMessageChannel Channel
        {
            get => _channel != null && _channel.Id == ChannelId ? _channel : (_channel = client.GetMessageChannel(ChannelId));
        }

        /// <summary>Retrieves this game's channel's guild. Null when the channel is a DM channel.</summary>
        public IGuild Guild => (Channel as IGuildChannel)?.Guild;


        /// <summary>Retrieves this game's latest message. Null if not retrievable.</summary>
        public async Task<IUserMessage> GetMessage()
        {
            if (MessageId == 0 || Channel == null) return null;
            if (_message == null || _message.Id != MessageId) return (_message = await Channel.GetUserMessageAsync(MessageId));
            return _message;
        }


        /// <summary>Empty game constructor, used only with reflection and serialization.</summary>
        protected ChannelGame() { }

        /// <summary>Creates a new game instance in the specified channel and with the specified players.</summary>
        protected ChannelGame(ulong channelId, ulong[] userIds, IServiceProvider services)
            : base(userIds, services)
        {
            ChannelId = channelId;
        }
    }
}
