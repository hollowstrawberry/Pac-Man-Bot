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
        /// <summary>Is a match when the given text begins with a mention to the bot's current user.</summary>
        public Regex MentionPrefix { get; private set; }

        public PmDiscordClient(PmConfig config) : base(config.ClientConfig)
        {
            ShardReady += OnShardReady;
        }

        private Task OnShardReady(DiscordSocketClient arg)
        {
            MentionPrefix = new Regex($@"^<@!?{CurrentUser.Id}>");
            ShardReady -= OnShardReady;
            return Task.CompletedTask;
        }
    }
}
