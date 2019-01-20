using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Discord;
using Discord.WebSocket;
using PacManBot.Utils;
using PacManBot.Services;
using PacManBot.Constants;
using PacManBot.Extensions;

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
