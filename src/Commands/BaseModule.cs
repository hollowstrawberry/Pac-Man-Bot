using System;
using System.Diagnostics.CodeAnalysis;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using PacManBot.Extensions;
using PacManBot.Services;

namespace PacManBot.Commands
{
    /// <summary>
    /// The base for Pac-Man Bot modules, including their main services and some utilities.
    /// </summary>
    public abstract class BaseModule : BaseCommandModule
    {
        /// <summary>Base permissions needed by all commands of the bot.</summary>
        protected const Permissions BaseBotPermissions =
            Permissions.ReadMessageHistory | Permissions.EmbedLinks |
            Permissions.UseExternalEmojis | Permissions.AddReactions;

        /// <summary>Invisible character to be used in embeds.</summary>
        protected const string Empty = DiscordStringUtilities.Empty;


        /// <summary>Runtime settings of the bot.</summary>
        public BotConfig Config { get; set; }
        /// <summary>Content used throughout the bot.</summary>
        public BotContent Content => Config.Content;
        /// <summary>All services used to create new games.</summary>
        public IServiceProvider Services { get; set; }
        /// <summary>The bot's overarching sharded client.</summary>
        public DiscordShardedClient ShardedClient { get; set; }
        /// <summary>Logs everything in the console and on disk.</summary>
        public LoggingService Log { get; set; }
        /// <summary>Gives access to the bot's database.</summary>
        public DatabaseService Storage { get; set; }
        /// <summary>Gives access to input manipulation.</summary>
        public InputService Input { get; set; }
        /// <summary>Gives access to active games.</summary>
        public GameService Games { get; set; }
    }
}
