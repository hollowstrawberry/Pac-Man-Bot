using System;
using System.Linq;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Discord;
using PacManBot.Constants;
using PacManBot.Extensions;

namespace PacManBot
{
    /// <summary>
    /// Contains the runtime settings of the bot loaded from file.
    /// </summary>
    [DataContract]
    public class BotConfig
    {
        /// <summary>Secret token used to connect to Discord. Must be provided for the bot to run.</summary>
        [DataMember] public readonly string discordToken;

        /// <summary>The prefix used for all guilds that don't set a custom prefix.</summary>
        [DataMember] public readonly string defaultPrefix = "<";

        /// <summary>User IDs of users to be considered developers and able to use developer commands. Dangerous.</summary>
        [DataMember] public readonly ulong[] developers = new ulong[0];

        /// <summary>The string that defines the connection to the SQLite database in <see cref="Services.StorageService"/>.</summary>
        [DataMember] public readonly string dbConnectionString = $"Data Source={Files.DefaultDatabase};";

        /// <summary>Secret tokens to send HTTP requests to bot list websites, to update guild count and such. Not required.</summary>
        [DataMember] public readonly string[] httpToken = { };

        /// <summary>Number of shards to divide the bot into. 1 shard per 1000 guilds is enough.</summary>
        [DataMember] public readonly int shardCount = 1;

        /// <summary>How many messages to keep on memory per channel. Keep it to a reasonable amount.</summary>
        [DataMember] public readonly int messageCacheSize = 100;

        /// <summary>How many messages to log from the client. See <see cref="LogSeverity"/> for possible values.</summary>
        [DataMember] public readonly LogSeverity clientLogLevel = LogSeverity.Verbose;

        /// <summary>How many messages to log from command calls. See <see cref="LogSeverity"/> for possible values.</summary>
        [DataMember] public readonly LogSeverity commandLogLevel = LogSeverity.Verbose;

        /// <summary>Strings that when found cause a log event to be ignored. Use with caution.</summary>
        [DataMember] public readonly string[] logExclude = new string[0];

        /// <summary>Until a long-term solution to command spam attacks is found, I can just ban channels from using the bot.</summary>
        [DataMember] public readonly ulong[] bannedChannels = new ulong[0];




        /// <summary>Content used throughout the bot. Set using <see cref="LoadContent(string)"/>.</summary>
        public BotContent Content { get; private set; }


        /// <summary>Reloads <see cref="Content"/> from the provided json.</summary>
        public void LoadContent(string json)
        {
            var cont = JsonConvert.DeserializeObject<BotContent>(json);

            var missingFields = typeof(BotContent).GetFields().Where(x => x.GetValue(cont) == null).ToList();
            if (missingFields.Count > 0)
            {
                throw new InvalidOperationException(
                    $"The contents file is missing a value for: {missingFields.Select(x => x.Name).JoinString(", ")}");
            }

            for (int i = 0; i < cont.aboutFields.Length; i++)
            {
                (string name, string desc) = cont.aboutFields[i];
                cont.aboutFields[i] = (name, desc.Replace("{version}", cont.version));
            }

            Content = cont;
        }
    }
}
