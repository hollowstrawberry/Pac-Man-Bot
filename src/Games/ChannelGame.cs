using System;
using System.Linq;
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


        private DiscordClient _client;
        private DiscordChannel _channel;
        private DiscordMessage _message;

        /// <summary>Retrieves the game's current shard.</summary>
        public DiscordClient Client
        {
            get
            {
                if (_client != null) return _client;

                foreach (var shard in shardedClient.ShardClients.Values)
                {
                    if (shard.PrivateChannels.TryGetValue(ChannelId, out var dmChannel))
                    {
                        _channel = dmChannel;
                        return _client = shard;
                    }

                    foreach (var guild in shard.Guilds.Values)
                    {
                        if (guild.Channels.TryGetValue(ChannelId, out _channel))
                        {
                            return _client = shard;
                        }
                    }
                }
                return null;
            }
        }

        /// <summary>Returns the game's current channel.</summary>
        public DiscordChannel Channel => Client == null ? null : _channel;

        /// <summary>Retrieves this game's guild. Null when the channel is a DM channel.</summary>
        public DiscordGuild Guild => Channel?.Guild;

        /// <summary>Returns the game's current message, caching it if it wasn't already.</summary>
        public async ValueTask<DiscordMessage> GetMessageAsync()
        {
            if (Channel != null && _message?.Id != MessageId)
            {
                _message = await Channel.GetMessageAsync(MessageId);
            }
            return _message;
        }

        /// <summary>Retrieves this game's owner (its first player).</summary>
        public override async ValueTask<DiscordUser> GetOwnerAsync()
        {
            return Guild == null ? await Client.GetUserAsync(OwnerId) : await Guild.GetMemberAsync(OwnerId);
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
        protected string StripPrefix(string value)
        {
            string prefix = storage.GetPrefix(Channel);
            return value.StartsWith(prefix) ? value.Substring(prefix.Length) : value;
        }
    }
}
