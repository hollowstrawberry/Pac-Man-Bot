using System;
using System.Linq;
using System.Runtime.Serialization;
using DSharpPlus;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PacManBot.Constants;
using PacManBot.Extensions;
using PacManBot.Services;

namespace PacManBot
{
    /// <summary>
    /// Contains the runtime settings of the bot.
    /// </summary>
    [DataContract]
    public class PmBotConfig
    {
        /// <summary>Secret token used to connect to Discord. Must be provided for the bot to run.</summary>
        [DataMember] public readonly string discordToken;

        /// <summary>Secret token to send requests to top.gg</summary>
        [DataMember] public readonly string discordBotListToken;

        /// <summary>The prefix used for all guilds that don't set a custom prefix.</summary>
        [DataMember] public readonly string defaultPrefix = "<";

        /// <summary>The string that defines the connection to the SQLite database in <see cref="Services.DatabaseService"/>.</summary>
        [DataMember] public readonly string dbConnectionString = $"Data Source={Files.DefaultDatabase};";

        /// <summary>Number of shards to divide the bot into. 1 shard per 1000 guilds is enough.</summary>
        [DataMember] public readonly int shardCount = 1;

        /// <summary>Whether the bot should close at midnight, in order for the OS to handle its restart.</summary>
        [DataMember] public readonly bool scheduledRestart = false;

        /// <summary>How many messages to keep on memory per channel. Keep it to a reasonable amount.</summary>
        [DataMember] public readonly int messageCacheSize = 20;

        /// <summary>Sets the timeout for HTTP events.</summary>
        [DataMember] public readonly int httpTimeout = 10000;

        /// <summary>How many messages this program should log. See <see cref="LogSeverity"/> for possible values.</summary>
        [DataMember] public readonly LogLevel logLevel = LogLevel.Debug;

        /// <summary>How many messages to log coming from the Discord client. See <see cref="LogSeverity"/> for possible values.</summary>
        [DataMember] public readonly LogLevel clientLogLevel = LogLevel.Information;

        /// <summary>Strings that when found cause a log event to be ignored. Use with caution.</summary>
        [DataMember] public readonly string[] logExclude = Array.Empty<string>();

        /// <summary>Until a long-term solution to command spam attacks is found, I can just ban channels from using the bot.</summary>
        [DataMember] public readonly ulong[] bannedChannels = Array.Empty<ulong>();

        /// <summary>Whether to send a DM to the owner of the bot's Application on startup.</summary>
        [DataMember] public readonly bool messageOwnerOnStartup = true;




        /// <summary>Content used throughout the bot. Set using <see cref="LoadContent(string)"/>.</summary>
        public PmBotContent Content { get; private set; }


        /// <summary>Reloads <see cref="Content"/> from the provided json.</summary>
        public void LoadContent(string json)
        {
            var cont = JsonConvert.DeserializeObject<PmBotContent>(json);

            var missingFields = typeof(PmBotContent).GetFields().Where(x => x.GetValue(cont) is null).ToList();
            if (missingFields.Count > 0)
            {
                throw new InvalidOperationException(
                    $"The contents file is missing a value for: {missingFields.Select(x => x.Name).JoinString(", ")}");
            }

            Content = cont;
        }


        /// <summary>Gets a configuration object for a <see cref="DiscordSocketClient"/>.</summary>
        public DiscordConfiguration MakeClientConfig(LoggingService log)
        {
            return new DiscordConfiguration
            {
                Token = discordToken,
                HttpTimeout = TimeSpan.FromSeconds(httpTimeout),
                LoggerFactory = log,
                MinimumLogLevel = clientLogLevel,
                MessageCacheSize = messageCacheSize,

                Intents =
                DiscordIntents.Guilds | DiscordIntents.DirectMessages | DiscordIntents.DirectMessageReactions
                | DiscordIntents.GuildMembers | DiscordIntents.GuildMessages | DiscordIntents.GuildMessageReactions,
            };
        }
    }
}
