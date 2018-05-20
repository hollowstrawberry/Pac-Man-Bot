using System;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using PacManBot.Constants;
using PacManBot.Services;
using static PacManBot.Games.GameUtils;

namespace PacManBot.Games
{
    public abstract class BaseGame : IBaseGame
    {
        protected DiscordShardedClient client;
        protected StorageService storage;
        protected LoggingService logger;
        protected CancellationTokenSource discordRequestCTS = new CancellationTokenSource();

        public abstract string Name { get; }
        public abstract TimeSpan Expiry { get; }

        public virtual State State { get; set; }
        public virtual DateTime LastPlayed { get; set; }
        public virtual int Time { get; set; }
        public virtual ulong MessageId { get; set; }
        public virtual ulong ChannelId { get; set; }
        public virtual ulong[] UserId { get; set; }

        public ISocketMessageChannel Channel => client.GetChannel(ChannelId) as ISocketMessageChannel;
        public SocketGuild Guild => (client.GetChannel(ChannelId) as SocketGuildChannel)?.Guild;
        public async Task<IUserMessage> GetMessage() => (await Channel.GetMessageAsync(MessageId, options: Utils.DefaultOptions)) as IUserMessage;

        public RequestOptions RequestOptions => new RequestOptions() { Timeout = 10000, RetryMode = RetryMode.RetryRatelimit, CancelToken = discordRequestCTS.Token };

        public Action<MessageProperties> UpdateDisplay => (msg => {
            msg.Content = GetContent();
            msg.Embed = GetEmbed()?.Build();
        });



        protected BaseGame() { }

        protected BaseGame(ulong channelId, ulong[] userId, DiscordShardedClient client, LoggingService logger, StorageService storage)
        {
            this.client = client;
            this.logger = logger;
            this.storage = storage;
            ChannelId = channelId;
            UserId = userId;

            MessageId = 1;
            State = State.Active;
            Time = 0;
            LastPlayed = DateTime.Now;
        }



        public virtual string GetContent(bool showHelp = true) => "";


        public virtual EmbedBuilder GetEmbed(bool showHelp = true) => null;


        public virtual void CancelRequests()
        {
            discordRequestCTS.Cancel();
            discordRequestCTS = new CancellationTokenSource();
        }


        protected EmbedBuilder CancelledEmbed()
        {
            return new EmbedBuilder()
            {
                Title = Name,
                Description = DateTime.Now - LastPlayed > Expiry ? "Game timed out" : "Game cancelled",
                Color = Player.None.Color(),
            };
        }
    }
}
