using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using PacManBot.Services;

namespace PacManBot.Games
{
    public abstract class ChannelGame : BaseGame, IChannelGame
    {
        public virtual ulong ChannelId { get; set; }
        public virtual ulong MessageId { get; set; }

        public ISocketMessageChannel Channel => client.GetChannel(ChannelId) as ISocketMessageChannel;
        public SocketGuild Guild => (client.GetChannel(ChannelId) as SocketGuildChannel)?.Guild;
        public async Task<IUserMessage> GetMessage() => (await Channel.GetMessageAsync(MessageId, options: Utils.DefaultOptions)) as IUserMessage;


        protected ChannelGame() : base() { }

        protected ChannelGame(ulong channelId, ulong[] userId, DiscordShardedClient client, LoggingService logger, StorageService storage)
            : base(userId, client, logger, storage)
        {
            ChannelId = channelId;
            MessageId = 1;
        }
    }
}
