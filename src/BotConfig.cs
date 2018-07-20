using System.Runtime.Serialization;
using Discord;

namespace PacManBot
{
    /// <summary>
    /// Contains the runtime settings of the bot, loaded from file at startup.
    /// </summary>
    [DataContract]
    public class BotConfig
    {
        [DataMember] public readonly string discordToken;
        [DataMember] public readonly string defaultPrefix = "<";
        [DataMember] public readonly string[] httpToken = { };
        [DataMember] public readonly int shardCount = 1;
        [DataMember] public readonly int messageCacheSize = 100;
        [DataMember] public readonly LogSeverity clientLogLevel = LogSeverity.Verbose;
        [DataMember] public readonly LogSeverity commandLogLevel = LogSeverity.Verbose;
    }
}
