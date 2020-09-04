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

        /// <summary>Is a match when the given text begins with a mention to the bot's current user.</summary>
        public Regex MentionPrefix => regex ?? (regex = new Regex($@"^<@!?{CurrentUser.Id}>"));
    }
}
