using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using PacManBot.Extensions;
using PacManBot.Services;

namespace PacManBot.Commands
{
    /// <summary>
    /// The base for Pac-Man Bot modules, including their main services and some utilities.
    /// </summary>
    [RequireBotPermissions(Permissions.ReadMessageHistory | Permissions.EmbedLinks |
                           Permissions.UseExternalEmojis | Permissions.AddReactions)]
    public abstract class BasePmBotModule : BaseCommandModule
    {
        /// <summary>Invisible character to be used in embeds.</summary>
        protected const string Empty = DiscordStringUtilities.Empty;


        /// <summary>Runtime settings of the bot.</summary>
        public PmBotConfig Config { get; set; }
        /// <summary>Content used throughout the bot.</summary>
        public PmBotContent Content => Config.Content;
        /// <summary>The bot's overarching sharded client.</summary>
        public DiscordShardedClient ShardedClient { get; set; }
        /// <summary>Logs everything in the console and on disk.</summary>
        public LoggingService Log { get; set; }
        /// <summary>Gives access to the bot's database.</summary>
        public StorageService Storage { get; set; }
        /// <summary>Gives access to input manipulation.</summary>
        public InputService Input { get; set; }
    }
}
