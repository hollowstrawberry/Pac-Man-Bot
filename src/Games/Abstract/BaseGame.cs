using System;
using System.Threading;
using Discord;
using Discord.WebSocket;
using PacManBot.Services;
using PacManBot.Extensions;

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
        public virtual ulong[] UserId { get; set; }

        public virtual ulong OwnerId { get => UserId[0]; protected set => UserId = new[] { value }; }
        public virtual IUser Owner => client.GetUser(OwnerId);

        public RequestOptions RequestOptions => new RequestOptions {
            Timeout = 10000,
            RetryMode = RetryMode.RetryRatelimit,
            CancelToken = discordRequestCTS.Token
        };

        public Action<MessageProperties> UpdateMessage => msg => {
            msg.Content = GetContent();
            msg.Embed = GetEmbed()?.Build();
        };


        protected BaseGame() { }

        protected BaseGame(ulong[] userIds, IServiceProvider services)
        {
            SetServices(services);
            UserId = userIds;

            State = State.Active;
            Time = 0;
            LastPlayed = DateTime.Now;
        }


        protected virtual void SetServices(IServiceProvider services)
        {
            client = services.Get<DiscordShardedClient>();
            logger = services.Get<LoggingService>();
            storage = services.Get<StorageService>();
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
