using System;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using PacManBot.Extensions;

namespace PacManBot.Games
{
    /// <summary>
    /// A type of game that takes place in a specific channel. Implements <see cref="IChannelGame"/>.
    /// </summary>
    public abstract class ChannelGame : BaseGame, IChannelGame
    {
        /// <summary>Discord snowflake ID of the channel where this game is taking place in.</summary>
        public virtual ulong ChannelId { get; set; }
        /// <summary>Discord snowflake ID of the latest message used by this game.</summary>
        public virtual ulong MessageId { get; set; }
        /// <summary>The time when the message used by this game was last moved.</summary>
        public virtual DateTime LastBumped { get; set; }


        private DiscordClient client;
        private DiscordChannel channel;
        private DiscordMessage message;
        private DiscordUser owner;

        /// <summary>Returns the game's current shard, caching it if it wasn't already.</summary>
        public async ValueTask<DiscordClient> GetClientAsync()
        {
            if (client != null) return client;

            foreach (var (_, shard) in shardedClient.ShardClients)
            {
                channel = await shard.GetChannelAsync(ChannelId);
                if (channel != null)
                {
                    if (MessageId > 0) message = await channel.GetMessageAsync(MessageId);
                    return client = shard;
                }
            }
            return null;
        }

        /// <summary>Returns the game's current channel, caching it if it wasn't already.</summary>
        public async ValueTask<DiscordChannel> GetChannelAsync()
        {
            if (client == null) await GetClientAsync();
            return channel;
        }

        /// <summary>Returns the game's current message, caching it if it wasn't already.</summary>
        public async ValueTask<DiscordMessage> GetMessageAsync()
        {
            if (client == null) await GetClientAsync();
            if (channel != null && message?.Id != MessageId)
            {
                message = await channel.GetMessageAsync(MessageId);
            }
            return message;
        }

        /// <summary>Retrieves this game's guild. Null when the channel is a DM channel.</summary>
        public async ValueTask<DiscordGuild> GetGuildAsync()
        {
            if (client == null) await GetClientAsync();
            return channel?.Guild;
        }

        /// <summary>Retrieves this game's owner (its first player).</summary>
        public override async ValueTask<DiscordUser> GetOwnerAsync()
        {
            if (owner != null) return owner;
            var guild = await GetGuildAsync();
            return owner = (guild == null ? await client.GetUserAsync(OwnerId) : await guild.GetMemberAsync(OwnerId));
        }


        /// <summary>Empty game constructor, used only with reflection and serialization.</summary>
        protected ChannelGame() { }

        /// <summary>Creates a new game instance in the specified channel and with the specified players.</summary>
        protected ChannelGame(ulong channelId, ulong[] userIds, IServiceProvider services)
            : base(userIds, services)
        {
            ChannelId = channelId;
        }


        /// <summary>Used to remove the guild prefix from game input, as it is to be ignored.</summary>
        protected async ValueTask<string> StripPrefixAsync(string value)
        {
            string prefix = storage.GetPrefix(await GetChannelAsync());
            return value.StartsWith(prefix) ? value.Substring(prefix.Length) : value;
        }
    }
}
