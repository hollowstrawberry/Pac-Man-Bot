using System;
using Discord.Commands;
using Discord.WebSocket;
using PacManBot.Extensions;
using PacManBot.Services;

namespace PacManBot.Commands
{
    /// <summary>
    /// The context of a Pac-Man Bot command, including the client, user, guild, channel, message, prefix, etc.
    /// </summary>
    public class PmCommandContext : ShardedCommandContext
    {
        public PmCommandContext(SocketUserMessage msg, IServiceProvider services)
            : base(services.Get<PmDiscordClient>(), msg)
        {
            var storage = services.Get<StorageService>();
            FixedPrefix = storage.GetGuildPrefix(Guild);
            Prefix = storage.RequiresPrefix(this) ? FixedPrefix : "";
        }

        /// <summary>Gets the <see cref="PmDiscordClient"/> that the command is executed with.</summary>
        public new PmDiscordClient Client => (PmDiscordClient)base.Client;

        /// <summary>The prefix accepted in this context, even if none is necessary.</summary>
        public string FixedPrefix { get; }
        /// <summary>The relative prefix used in this context. Might be empty in DMs and other cases.</summary>
        public string Prefix { get; }
    }
}
