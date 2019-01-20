using Discord.WebSocket;

namespace PacManBot.Services
{
    /// <summary>
    /// The sharded Discord client used by the bot.
    /// </summary>
    public class PmDiscordClient : DiscordShardedClient
    {
        public PmDiscordClient(PmConfig config) : base(config.ClientConfig) { }
    }
}
