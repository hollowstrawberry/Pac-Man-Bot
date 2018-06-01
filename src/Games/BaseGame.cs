using System;
using System.Threading;
using Discord;
using Discord.WebSocket;
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
        public virtual ulong[] UserId { get; set; }

        public RequestOptions RequestOptions => new RequestOptions() { Timeout = 10000, RetryMode = RetryMode.RetryRatelimit, CancelToken = discordRequestCTS.Token };

        public Action<MessageProperties> UpdateMessage => (msg => {
            msg.Content = GetContent();
            msg.Embed = GetEmbed()?.Build();
        });


        protected BaseGame() { }

        protected BaseGame(ulong[] userId, DiscordShardedClient client, LoggingService logger, StorageService storage)
        {
            this.client = client;
            this.logger = logger;
            this.storage = storage;
            UserId = userId;

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
