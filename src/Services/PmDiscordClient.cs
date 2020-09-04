using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord.WebSocket;

namespace PacManBot.Services
{
    /// <summary>
    /// The sharded Discord client used by PacManBot.
    /// </summary>
    public class PmDiscordClient : DiscordShardedClient
    {
        private Regex regex;
        private int shardsReady;

        /// <summary>Is a match when the given text begins with a mention to the bot's current user.</summary>
        public Regex MentionPrefix => regex ?? (regex = new Regex($@"^<@!?{CurrentUser.Id}>"));

        /// <summary>Fired when guild data for all shards has finished downloading.</summary>
        public event Func<Task> AllShardsReady;


        public PmDiscordClient(PmConfig config)
            : base(config.ClientConfig)
        {
            ShardReady += OnShardReady;
        }


        private async Task OnShardReady(DiscordSocketClient shard)
        {
            if (++shardsReady == Shards.Count)
            {
                if (AllShardsReady != null) await AllShardsReady.Invoke();
            }
        }
    }
}
