using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.Exceptions;
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

        /// <summary>The last time this game's message was updated.</summary>
        protected DateTime LastUpdated { get; set; } = default;

        /// <summary>Unique objects that identify requests to update this game's message</summary>
        protected ConcurrentStack<object> PendingUpdates { get; } = new ConcurrentStack<object>();

        /// <summary>Empty game constructor, used only with reflection and serialization.</summary>
        protected ChannelGame() { }

        /// <summary>Creates a new game instance in the specified channel and with the specified players.</summary>
        protected ChannelGame(ulong channelId, ulong[] userIds, IServiceProvider services)
            : base(userIds, services)
        {
            ChannelId = channelId;
        }


        private DiscordClient _client;

        /// <summary>Retrieves the game's current shard.</summary>
        public DiscordClient Client
        {
            get
            {
                if (_client is not null && ChannelId == _channel?.Id) return _client;
                if (ChannelId <= 0) return ShardedClient.ShardClients.Values.First();

                foreach (var shard in ShardedClient.ShardClients.Values)
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
                return ShardedClient.ShardClients.Values.First();
            }
        }


        private DiscordChannel _channel;

        /// <summary>Retrieves the game's current channel.</summary>
        public DiscordChannel Channel
        {
            get
            {
                if (_channel?.Id != ChannelId) // if the channel changed, all bets are off
                {
                    foreach (var shard in ShardedClient.ShardClients.Values)
                    {
                        if (shard.PrivateChannels.TryGetValue(ChannelId, out var dmChannel))
                        {
                            _client = shard;
                            return _channel = dmChannel;
                        }

                        foreach (var guild in shard.Guilds.Values)
                        {
                            if (guild.Channels.TryGetValue(ChannelId, out _channel))
                            {
                                _client = shard;
                                return _channel;
                            }
                        }
                    }
                }
                return _channel;
            }
        }


        /// <summary>Retrieves this game's guild. Null when the channel is a DM channel.</summary>
        public DiscordGuild Guild => Channel?.Guild;


        private DiscordMessage _message;

        /// <summary>Returns the game's current message, caching it if it wasn't already.</summary>
        public async ValueTask<DiscordMessage> GetMessageAsync()
        {
            if (Channel is not null && _message?.Id != MessageId)
            {
                try { _message = await Channel.GetMessageAsync(MessageId); }
                catch (NotFoundException) { return _message = null; }
            }
            return _message;
        }


        /// <summary>Retrieves this game's owner (its first player).</summary>
        public override async ValueTask<DiscordUser> GetOwnerAsync()
        {
            return Guild is null ? await Client.GetUserAsync(OwnerId) : await Guild.GetMemberAsync(OwnerId);
        }


        /// <summary>Used to remove the guild prefix from game input, as it is to be ignored.</summary>
        protected string StripPrefix(string value)
        {
            string prefix = Storage.GetPrefix(Channel);
            return value.StartsWith(prefix) ? value[prefix.Length..] : value;
        }


        private const int UpdateMillis = 1000;

        /// <summary>Schedules the updating of this game's message, be it editing or creating it.
        /// Manages multiple calls close together.</summary>
        /// <param name="gameMessage">Optionally, the game's own pre-fetched message</param>
        /// <param name="inputMessage">A DiscordMessage that called for this update, to be deleted afterwards.</param>
        public async Task UpdateMessageAsync(DiscordMessage gameMessage = null, DiscordMessage inputMessage = null)
        {
            if (gameMessage is null) gameMessage = await GetMessageAsync();

            var updateKey = (object)inputMessage ?? DateTime.Now;
            PendingUpdates.Push(updateKey);
            
            if (DateTime.Now - LastUpdated > TimeSpan.FromMilliseconds(UpdateMillis))
            {
                await EditOrCreateMessageAsync(gameMessage);
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(UpdateMillis) - (DateTime.Now - LastUpdated));

            if (PendingUpdates.TryPeek(out object latest) && latest == updateKey)
            {
                await EditOrCreateMessageAsync(gameMessage);
            }
        }

        private async Task EditOrCreateMessageAsync(DiscordMessage gameMessage)
        {
            LastUpdated = DateTime.Now;
            var updates = PendingUpdates.ToList();
            PendingUpdates.Clear();

            if (gameMessage is not null && (this is IReactionsGame || Channel.BotCan(Permissions.ManageMessages)))
            {
                await gameMessage.ModifyAsync(await GetContentAsync(), (await GetEmbedAsync())?.Build());

                if (Channel is not null)
                {
                    var inputMessages = updates.OfType<DiscordMessage>().ToList();
                    if (inputMessages.Count > 1) await Channel.DeleteMessagesAsync(inputMessages);
                    else if (inputMessages.Count == 1) await Channel.DeleteMessageAsync(inputMessages.First());
                }
            }
            else
            {
                var newMsg = await gameMessage.Channel
                    .SendMessageAsync(await GetContentAsync(), false, (await GetEmbedAsync())?.Build());
                MessageId = newMsg.Id;

                if (gameMessage is not null) await gameMessage.DeleteAsync();
            }

            LastUpdated = DateTime.Now;
        }
    }
}
