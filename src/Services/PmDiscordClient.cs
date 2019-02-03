using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord.WebSocket;

namespace PacManBot.Services
{
    /// <summary>
    /// The sharded Discord client used by the bot.
    /// </summary>
    public class PmDiscordClient : DiscordShardedClient
    {
        private Regex internalRegex;

        /// <summary>Is a match when the given text begins with a mention to the bot's current user.</summary>
        public Regex MentionPrefix => internalRegex ?? (internalRegex = new Regex($@"^<@!?{CurrentUser.Id}>"));

        public PmDiscordClient(PmConfig config) : base(config.ClientConfig) { }
    }
}
