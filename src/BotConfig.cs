using System;
using System.Linq;
using System.Runtime.Serialization;
using DSharpPlus;
using DSharpPlus.Entities;
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
    public class BotConfig
    {
        /// <summary>Secret token used to connect to Discord. Must be provided for the bot to run.</summary>
        [DataMember] internal readonly string discordToken = null;

        /// <summary>Secret token to send requests to top.gg</summary>
        [DataMember] internal readonly string discordBotListToken = null;

        /// <summary>The prefix used for all guilds that don't set a custom prefix.</summary>
        [DataMember] public readonly string defaultPrefix = "<";

        /// <summary>The string that defines the connection to the SQLite database in <see cref="DatabaseService"/>.</summary>
        [DataMember] public readonly string dbConnectionString = $"Data Source={Files.DefaultDatabase};";

        /// <summary>Whether the bot should close at midnight, in order for the OS to handle its restart.</summary>
        [DataMember] public readonly bool scheduledRestart = false;

        /// <summary>How many messages to keep on memory per channel. Keep it to a reasonable amount.</summary>
        [DataMember] public readonly int messageCacheSize = 20;

        /// <summary>Sets the timeout for HTTP events.</summary>
        [DataMember] public readonly int httpTimeout = 10000;

        /// <summary>How many messages this program should log. See <see cref="LogLevel"/> for possible values.</summary>
        [DataMember] public readonly LogLevel logLevel = LogLevel.Debug;

        /// <summary>How many messages to log coming from the Discord client. See <see cref="LogLevel"/> for possible values.</summary>
        [DataMember] public readonly LogLevel clientLogLevel = LogLevel.Information;

        /// <summary>The serilog log message template.</summary>
        [DataMember] public readonly string logTemplate = "[{Timestamp:HH:mm:ss}][{Level:u3}] {Message}{NewLine}";

        /// <summary>Strings that when found cause a log event to be ignored. Use with caution.</summary>
        [DataMember] public readonly string[] logExclude = Array.Empty<string>();

        /// <summary>Until a long-term solution to command spam attacks is found, I can just ban channels from using the bot.</summary>
        [DataMember] public readonly ulong[] bannedChannels = Array.Empty<ulong>();

        /// <summary>The support server for this bot and whereits owner can be found.</summary>
        [DataMember] public readonly ulong ownerGuild = 409803292219277313;

        /// <summary>A message to DM to the bot's owner on startup.</summary>
        [DataMember] public readonly string ownerStartupMessage = "";

        /// <summary>What message to show as a status from the moment the bot starts.</summary>
        [DataMember] public readonly ActivityType statusType = ActivityType.Playing;

        /// <summary>What message to show as a status from the moment the bot starts.</summary>
        [DataMember] public readonly string status = "with you!";




        /// <summary>Static data used throughout the bot. Loaded using <see cref="LoadContent(string)"/>.</summary>
        public BotContent Content { get; private set; }


        /// <summary>Loads <see cref="Content"/> from the provided json.</summary>
        public void LoadContent(string json)
        {
            var cont = JsonConvert.DeserializeObject<BotContent>(json);

            var missingFields = typeof(BotContent).GetFields().Where(x => x.GetValue(cont) is null).ToList();
            if (missingFields.Count > 0)
            {
                throw new InvalidOperationException(
                    $"The contents file is missing a value for: {missingFields.Select(x => x.Name).JoinString(", ")}");
            }

            Content = cont;
        }


        /// <summary>Gets a configuration object for a <see cref="DiscordClient"/>.</summary>
        public DiscordConfiguration MakeDiscordConfig(LoggingService log)
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
