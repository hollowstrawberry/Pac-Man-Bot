using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using PacManBot.Utils;
using PacManBot.Services.Database;
using PacManBot.Constants;
using PacManBot.Extensions;

namespace PacManBot.Services
{
    /// <summary>
    /// Manages access to the bot's database.
    /// </summary>
    public class StorageService
    {
        private readonly DiscordShardedClient client;
        private readonly LoggingService logger;
        private readonly string dbConnection;


        private readonly ConcurrentDictionary<ulong, string> cachedPrefixes;
        private readonly ConcurrentDictionary<ulong, bool> cachedAllowsAutoresponse;
        private readonly ConcurrentDictionary<ulong, bool> cachedNeedsPrefix;

        public string DefaultPrefix { get; }


        private PacManDbContext MakeDbContext() => new PacManDbContext(dbConnection);


        public StorageService(BotConfig config, DiscordShardedClient client, LoggingService logger)
        {
            this.client = client;
            this.logger = logger;

            DefaultPrefix = config.defaultPrefix;
            dbConnection = config.dbConnectionString;

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
        public string GetGuildPrefix(IGuild guild) => guild == null ? DefaultPrefix : GetGuildPrefix(guild.Id);

        /// <summary>Retrieves the specified guild's custom prefix, or the default prefix if no record is found.</summary>
        public string GetGuildPrefix(ulong guildId)
        {
            if (cachedPrefixes.TryGetValue(guildId, out string prefix)) return prefix;

            using (var db = MakeDbContext())
            {
                prefix = db.Prefixes.Find(guildId)?.Prefix ?? DefaultPrefix;
            }

            cachedPrefixes.TryAdd(guildId, prefix);
            return prefix;
        }

        /// <summary>Retrieves the specified guild's custom prefix, or the default prefix if no record is found.
        /// Provides the benefit of an asynchronous database access if one is necessary.</summary>
        public async Task<string> GetGuildPrefixAsync(IGuild guild)
        {
            if (guild == null) return DefaultPrefix;
            if (cachedPrefixes.TryGetValue(guild.Id, out string prefix)) return prefix;

            using (var db = MakeDbContext())
            {
                prefix = (await db.Prefixes.FindAsync(guild.Id))?.Prefix ?? DefaultPrefix;
            }

            cachedPrefixes.TryAdd(guild.Id, prefix);
            return prefix;
        }


        /// <summary>Changes the prefix of the specified guild.</summary>
        public void SetGuildPrefix(ulong guildId, string prefix)
        {
            using (var db = MakeDbContext())
            {
                var entry = db.Prefixes.Find(guildId);

                if (entry == null)
                {
                    if (prefix != DefaultPrefix) db.Prefixes.Add((guildId, prefix));
                }
                else
                {
                    if (prefix == DefaultPrefix) db.Prefixes.Remove(entry);
                    else entry.Prefix = prefix;
                }

                db.SaveChanges();
                cachedPrefixes[guildId] = prefix;
            }
        }


        /// <summary>Whether the specified context requires a prefix for commands.</summary>
        public bool RequiresPrefix(ICommandContext context) => RequiresPrefix(context.Channel);

        /// <summary>Whether the specified channel requires a prefix for commands.</summary>
        public bool RequiresPrefix(ulong channelId) => RequiresPrefix(client.GetChannel(channelId));

        /// <summary>Whether the specified channel requires a prefix for commands.</summary>
        public bool RequiresPrefix(IChannel channel)
        {
            if (cachedNeedsPrefix.TryGetValue(channel.Id, out bool needs)) return needs;

            using (var db = MakeDbContext())
            {
                needs = channel is IGuildChannel && db.NoPrefixGuildChannels.Find(channel.Id) == null;
            }

            cachedNeedsPrefix.TryAdd(channel.Id, needs);
            return needs;
        }

        /// <summary>Whether the specified channel requires a prefix for commands.
        /// Provides the benefit of an asynchronous database access if it is necessary.</summary>
        public async Task<bool> RequiresPrefixAsync(IChannel channel)
        {
            if (cachedNeedsPrefix.TryGetValue(channel.Id, out bool needs)) return needs;

            if (channel is IGuildChannel)
            {
                using (var db = MakeDbContext())
                {
                    needs = await db.NoPrefixGuildChannels.FindAsync(channel.Id) == null;
                }
            }
            else needs = false;
            
            cachedNeedsPrefix.TryAdd(channel.Id, needs);
            return needs;
        }


        /// <summary>Toggles the specified guild channel between requiring a prefix for commands and not, and returns the new value.</summary>
        public bool ToggleChannelGuildPrefix(ulong channelId)
        {
            using (var db = MakeDbContext())
            {
                var entry = db.NoPrefixGuildChannels.Find(channelId);

                if (entry == null) db.NoPrefixGuildChannels.Add(channelId);
                else db.NoPrefixGuildChannels.Remove(entry);

                db.SaveChanges();

                var nowNeeds = entry != null;
                cachedNeedsPrefix[channelId] = nowNeeds;
                return nowNeeds;
            }
        }



        /// <summary>Whether the specified guild is set to allow message autoresponses.</summary>
        public bool AllowsAutoresponse(IGuild guild) => guild == null ? true : AllowsAutoresponse(guild.Id);

        /// <summary>Whether the specified guild is set to allow message autoresponses.</summary>
        public bool AllowsAutoresponse(ulong guildId)
        {
            if (cachedAllowsAutoresponse.TryGetValue(guildId, out bool allows)) return allows;

            using (var db = MakeDbContext())
            {
                allows = db.NoAutoresponseGuilds.Find(guildId) == null;
            }

            cachedAllowsAutoresponse.TryAdd(guildId, allows);
            return allows;
        }

        /// <summary>Whether the specified guild is set to allow message autoresponses.
        /// Provides the benefit of an asynchronous database access if it is necessary.</summary>
        public async Task<bool> AllowsAutoresponseAsync(IGuild guild)
        {
            if (guild == null) return true;
            if (cachedAllowsAutoresponse.TryGetValue(guild.Id, out bool allows)) return allows;

            using (var db = MakeDbContext())
            {
                allows = await db.NoAutoresponseGuilds.FindAsync(guild.Id) == null;
            }

            cachedAllowsAutoresponse.TryAdd(guild.Id, allows);
            return allows;
        }


        /// <summary>Toggles message autoresponses on or off in the specified guild and returns the new value.</summary>
        public bool ToggleAutoresponse(ulong guildId)
        {
            using (var db = MakeDbContext())
            {
                var entry = db.NoAutoresponseGuilds.Find(guildId);

                if (entry == null) db.NoAutoresponseGuilds.Add(guildId);
                else db.NoAutoresponseGuilds.Remove(entry);

                db.SaveChanges();

                var nowAllows = entry != null;
                cachedAllowsAutoresponse[guildId] = nowAllows;
                return nowAllows;
            }
        }




        /// <summary>Adds a new entry to the <see cref="Games.Concrete.PacManGame"/> scoreboard.</summary>
        public void AddScore(ScoreEntry entry)
        {
            using (var db = MakeDbContext())
            {
                db.PacManScores.Add(entry);
                db.SaveChanges();
            }

            logger.Log(LogSeverity.Info, LogSource.Storage, $"New scoreboard entry: {entry}");
        }


        /// <summary>Retrieves a list of scores from the database that fulfills the specified requirements.</summary>
        public List<ScoreEntry> GetScores(TimePeriod period, int start = 0, int amount = 1, ulong? userId = null)
        {
            using (var db = MakeDbContext())
            {
                IQueryable<ScoreEntry> scores = db.PacManScores;

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
}
