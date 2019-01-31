using System;
using System.Threading;
using Discord;
using PacManBot.Extensions;
using PacManBot.Services;

namespace PacManBot.Games
{
    /// <summary>
    /// The base all games inherit from. Implements <see cref="IBaseGame"/>.
    /// </summary>
    public abstract class BaseGame : IBaseGame
    {
        /// <summary>Invisible character to be used in embeds.</summary>
        protected const string Empty = DiscordExtensions.Empty;

        protected PmDiscordClient client;
        protected PmConfig config;
        protected LoggingService log;
        protected StorageService storage;
        protected GameService games;

        protected PmContent Content => config.Content;

        /// <summary>Used to manage previous game tasks such as 
        /// ongoing Discord message editions to prevent them from piling up when new ones come in.</summary>
        protected CancellationTokenSource discordRequestCTS = new CancellationTokenSource();


        /// <summary>The display name of this game's type.</summary>
        public abstract string GameName { get; }

        /// <summary>The internal ID of this game's type used when sorting games.</summary>
        public abstract int GameIndex { get; }

        /// <summary>Time after which a game will be routinely deleted due to inactivity.</summary>
        public abstract TimeSpan Expiry { get; }



        /// <summary>The state indicating whether a game is ongoing, or why it ended.</summary>
        public virtual GameState State { get; set; }

        /// <summary>Date when this game was last accessed by a player.</summary>
        public virtual DateTime LastPlayed { get; set; }

        /// <summary>Individual actions taken since the game was created. It can mean different things for different games.</summary>
        public virtual int Time { get; set; }

        /// <summary>Discord snowflake ID of all users participating in this game.</summary>
        public virtual ulong[] UserId { get; set; }



        /// <summary>Discord snowflake ID of the first user of this game, or its owner in case of <see cref="IUserGame"/>s.</summary>
        public virtual ulong OwnerId { get => UserId[0]; protected set => UserId = new[] { value }; }

        /// <summary>Retrieves the first user of this game, or its owner in case of <see cref="IUserGame"/>s.</summary>
        public virtual IUser Owner => client.GetUser(OwnerId);




        /// <summary>Empty constructor used only in reflection and serialization.</summary>
        protected BaseGame() { }

        /// <summary>Creates a new game instance with the specified players.</summary>
        protected BaseGame(ulong[] userIds, IServiceProvider services)
        {
            SetServices(services);
            UserId = userIds;

            State = GameState.Active;
            Time = 0;
            LastPlayed = DateTime.Now;
        }


        /// <summary>Sets the services that will be used by this game instance.</summary>
        protected virtual void SetServices(IServiceProvider services)
        {
            config = services.Get<PmConfig>();
            client = services.Get<PmDiscordClient>();
            log = services.Get<LoggingService>();
            storage = services.Get<StorageService>();
            games = services.Get<GameService>();
        }


        /// <summary>Creates an updated string content for this game, to be put in a message.</summary>
        public virtual string GetContent(bool showHelp = true) => "";

        /// <summary>Creates an updated message embed for this game.</summary>
        public virtual EmbedBuilder GetEmbed(bool showHelp = true) => null;

        /// <summary>Creates a <see cref="RequestOptions"/> to be used in Discord tasks related to this game,
        /// such as message editions. Tasks with these options can be cancelled using <see cref="CancelRequests"/>.</summary>
        public RequestOptions GetRequestOptions() => new RequestOptions
        {
            Timeout = 10000,
            RetryMode = RetryMode.RetryRatelimit,
            CancelToken = discordRequestCTS.Token
        };

        /// <summary>Creates a delegate to be passed to <see cref="IUserMessage.ModifyAsync(Action{MessageProperties}, RequestOptions)"/>.
        /// The message will be updated with the latest content and embed from this game.</summary>
        public Action<MessageProperties> GetMessageUpdate() => msg => {
            msg.Content = GetContent();
            msg.Embed = GetEmbed()?.Build();
        };


        /// <summary>Cancels all previous Discord requests made using the options from <see cref="GetRequestOptions"/>.</summary>
        public virtual void CancelRequests()
        {
            discordRequestCTS.Cancel();
            discordRequestCTS = new CancellationTokenSource();
        }


        /// <summary>Creates a default message embed to be used when a game has timed out or been manually cancelled.</summary>
        protected EmbedBuilder CancelledEmbed()
        {
            return new EmbedBuilder()
            {
                Title = GameName,
                Description = DateTime.Now - LastPlayed > Expiry ? "Game timed out" : "Game cancelled",
                Color = Player.None.Color,
            };
        }
    }
}
