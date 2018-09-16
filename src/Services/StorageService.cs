using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Discord;
using Discord.Rest;
using Discord.Commands;
using Discord.WebSocket;
using PacManBot.Utils;
using PacManBot.Constants;
using PacManBot.Extensions;
using PacManBot.Services.Database;

namespace PacManBot.Services
{
    /// <summary>
    /// Manages access to the bot's database.
    /// </summary>
    public class StorageService
    {
        private readonly DiscordShardedClient client;
        private readonly LoggingService logger;


        private readonly ThreadLocal<PacManDbContext> threadedDb;
        private readonly ConcurrentDictionary<ulong, string> cachedPrefixes;
        private readonly ConcurrentDictionary<ulong, bool> cachedAllowsAutoresponse;
        private readonly ConcurrentDictionary<ulong, bool> cachedNeedsPrefix;

        private PacManDbContext Db => threadedDb.Value;

        public string DefaultPrefix { get; }



        public StorageService(BotConfig config, DiscordShardedClient client, LoggingService logger)
        {
            this.client = client;
            this.logger = logger;

            threadedDb = new ThreadLocal<PacManDbContext>(() => new PacManDbContext(config.dbConnectionString));

            DefaultPrefix = config.defaultPrefix;

            cachedPrefixes = new ConcurrentDictionary<ulong, string>();
            cachedAllowsAutoresponse = new ConcurrentDictionary<ulong, bool>();
            cachedNeedsPrefix = new ConcurrentDictionary<ulong, bool>();
        }




        /// <summary>Retrieves the prefix used in a particular context, or an empty string if none is necessary.</summary>
        public string GetPrefix(ICommandContext context) => GetPrefix(context.Channel);

        /// <summary>Retrieves the prefix used in a particular channel, or an empty string if none is necessary.</summary>
        public string GetPrefix(ulong channelId) => GetPrefix(client.GetMessageChannel(channelId));

        /// <summary>Retrieves the prefix used in a particular channel, or an empty string if none is necessary.</summary>
        public string GetPrefix(IMessageChannel channel)
        {
            return RequiresPrefix(channel) ? GetGuildPrefix((channel as IGuildChannel)?.Guild) : "";
        }


        /// <summary>Retrieves the specified guild's custom prefix, or the default prefix if no record is found.</summary>
        public string GetGuildPrefix(IGuild guild) => GetGuildPrefix(guild?.Id ?? 0);

        /// <summary>Retrieves the specified guild's custom prefix, or the default prefix if no record is found.</summary>
        public string GetGuildPrefix(ulong guildId)
        {
            if (cachedPrefixes.TryGetValue(guildId, out string prefix)) return prefix;

            prefix = Db.Prefixes.Find(guildId)?.Prefix ?? DefaultPrefix;

            cachedPrefixes.TryAdd(guildId, prefix);
            return prefix;
        }


        /// <summary>Changes the prefix of the specified guild.</summary>
        public void SetGuildPrefix(ulong guildId, string prefix)
        {
            string old = cachedPrefixes[guildId];

            if (prefix == DefaultPrefix) Db.Prefixes.Remove((guildId, prefix));
            else
            {
                if (old == DefaultPrefix) Db.Prefixes.Add((guildId, prefix));
                else Db.Prefixes.Find(guildId).Prefix = prefix;
            }

            Db.SaveChanges();
            cachedPrefixes[guildId] = prefix;
        }


        /// <summary>Whether the specified context requires a prefix for commands.</summary>
        public bool RequiresPrefix(ICommandContext context) => RequiresPrefix(context.Channel);

        /// <summary>Whether the specified channel requires a prefix for commands.</summary>
        public bool RequiresPrefix(ulong channelId) => RequiresPrefix(client.GetChannel(channelId));

        /// <summary>Whether the specified channel requires a prefix for commands.</summary>
        public bool RequiresPrefix(IChannel channel)
        {
            ulong id = channel?.Id ?? 0;
            if (cachedNeedsPrefix.TryGetValue(id, out bool needs)) return needs;

            needs = channel is IGuildChannel && !Db.NoPrefixGuildChannels.Contains((NoPrefixGuildChannel)id);
            cachedNeedsPrefix.TryAdd(id, needs);

            return needs;
        }


        /// <summary>Toggles the specified guild channel between requiring a prefix for commands and not, and returns the new value.</summary>
        public bool ToggleChannelGuildPrefix(ulong channelId)
        {
            bool needsPrefix = cachedNeedsPrefix[channelId];

            if (needsPrefix) Db.NoPrefixGuildChannels.Add(channelId);
            else Db.NoPrefixGuildChannels.Remove(channelId);

            Db.SaveChanges();
            cachedNeedsPrefix[channelId] = !needsPrefix;
            return !needsPrefix;
        }



        /// <summary>Whether the specified guild is set to allow message autoresponses.</summary>
        public bool AllowsAutoresponse(IGuild guild) => AllowsAutoresponse(guild?.Id ?? 0);

        /// <summary>Whether the specified guild is set to allow message autoresponses.</summary>
        public bool AllowsAutoresponse(ulong guildId)
        {
            if (cachedAllowsAutoresponse.TryGetValue(guildId, out bool allows)) return allows;

            allows = !Db.NoAutoresponseGuilds.Contains((NoAutoresponseGuild)guildId);
            cachedAllowsAutoresponse.TryAdd(guildId, allows);

            return allows;
        }


        /// <summary>Toggles message autoresponses on or off in the specified guild and returns the new value.</summary>
        public bool ToggleAutoresponse(ulong guildId)
        {
            bool allows = cachedAllowsAutoresponse[guildId];

            if (allows) Db.NoAutoresponseGuilds.Add(guildId);
            else Db.NoAutoresponseGuilds.Remove(guildId);

            Db.SaveChanges();
            cachedAllowsAutoresponse[guildId] = !allows;
            return !allows;
        }




        /// <summary>Adds a new entry to the <see cref="Games.Concrete.PacManGame"/> scoreboard.</summary>
        public void AddScore(ScoreEntry entry)
        {
            logger.Log(LogSeverity.Info, LogSource.Storage, $"New scoreboard entry: {entry}");

            Db.PacManScores.Add(entry);
            Db.SaveChanges();
        }


        /// <summary>Retrieves a list of scores from the database that fulfills the specified requirements.</summary>
        public List<ScoreEntry> GetScores(TimePeriod period, int start = 0, int amount = 1, ulong? userId = null)
        {
            IQueryable<ScoreEntry> scores = Db.PacManScores;

            if (period != TimePeriod.All)
            {
                var minDate = DateTime.Now - TimeSpan.FromHours((int)period);
                scores = scores.Where(x => x.Date > minDate);
            }
            if (userId != null) scores = scores.Where(x => x.UserId == userId);

            var list = scores.OrderByDescending(x => x.Score).Skip(start).Take(amount).ToList();
            logger.Log(LogSeverity.Info, LogSource.Storage, $"Grabbed {list.Count} score entries");
            return list;
        }
    }
}
