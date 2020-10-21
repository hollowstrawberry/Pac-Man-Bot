using System;
using System.Threading;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
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
        protected const string Empty = DiscordStringUtilities.Empty;

        protected DiscordShardedClient ShardedClient { get; private set; }
        protected PmBotConfig Config { get; private set; }
        protected LoggingService Log { get; private set; }
        protected DatabaseService Storage { get; private set; }
        protected GameService Games { get; private set; }

        protected PmBotContent Content => Config.Content;



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
        public virtual ulong[] UserId { get; protected set; }

        /// <summary>Discord snowflake ID of the first user of this game, or its owner in case of <see cref="IUserGame"/>s.</summary>
        public virtual ulong OwnerId { get => UserId[0]; protected set => UserId = new ulong[] { value }; }

        private DiscordUser _owner;

        /// <summary>Retrieves the user whose game this is.</summary>
        public virtual async ValueTask<DiscordUser> GetOwnerAsync()
        {
            if (_owner != null) return _owner;
            foreach (var (_, shard) in ShardedClient.ShardClients)
            {
                if ((_owner = await shard.GetUserAsync(OwnerId)) != null) break; 
            }
            return _owner;
        }



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
            Config = services.Get<PmBotConfig>();
            ShardedClient = services.Get<DiscordShardedClient>();
            Log = services.Get<LoggingService>();
            Storage = services.Get<DatabaseService>();
            Games = services.Get<GameService>();
        }


        /// <summary>Creates an updated string content for this game, to be put in a message.</summary>
        public virtual ValueTask<string> GetContentAsync(bool showHelp = true) => new ValueTask<string>("");

        /// <summary>Creates an updated message embed for this game.</summary>
        public virtual ValueTask<DiscordEmbedBuilder> GetEmbedAsync(bool showHelp = true) => new ValueTask<DiscordEmbedBuilder>((DiscordEmbedBuilder)null);


        /// <summary>Creates a default message embed to be used when a game has timed out or been manually cancelled.</summary>
        protected DiscordEmbedBuilder CancelledEmbed()
        {
            return new DiscordEmbedBuilder()
            {
                Title = GameName,
                Description = DateTime.Now - LastPlayed > Expiry ? "Game timed out" : "Game cancelled",
                Color = Player.None.Color,
            };
        }
    }
}
