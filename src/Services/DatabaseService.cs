using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.Entities;
using PacManBot.Services.Database;
using PacManBot.Utils;

namespace PacManBot.Services
{
    /// <summary>
    /// Manages access to the bot's database.
    /// </summary>
    public class DatabaseService
    {
        private readonly LoggingService _log;
        private readonly string _dbConnection;

        private readonly ConcurrentDictionary<ulong, string> _cachedPrefixes;
        private readonly ConcurrentDictionary<ulong, bool> _cachedNeedsPrefix;

        public string DefaultPrefix { get; }


        private PacManDbContext MakeDbContext() => new PacManDbContext(_dbConnection);


        public DatabaseService(PmBotConfig config, LoggingService log)
        {
            _log = log;

            DefaultPrefix = config.defaultPrefix;
            _dbConnection = config.dbConnectionString;

            _cachedPrefixes = new ConcurrentDictionary<ulong, string>();
            _cachedNeedsPrefix = new ConcurrentDictionary<ulong, bool>();

            using (var db = MakeDbContext())
            {
                db.Database.EnsureCreated();
                db.Prefixes.Find((ulong)0);
                log.Info("Database ready");
            }
        }



        /// <summary>Retrieves the prefix used in a particular context, or an empty string if none is necessary.</summary>
        public string GetPrefix(CommandContext context) => GetPrefix(context?.Channel);

        /// <summary>Retrieves the prefix used in a particular channel, or an empty string if none is necessary.</summary>
        public string GetPrefix(DiscordChannel channel)
        {
            return RequiresPrefix(channel) ? GetGuildPrefix(channel.Guild) : "";
        }


        /// <summary>Retrieves the specified guild's custom prefix, or the default prefix if no record is found.</summary>
        public string GetGuildPrefix(DiscordGuild guild) => guild == null ? DefaultPrefix : GetGuildPrefix(guild.Id);

        /// <summary>Retrieves the specified guild's custom prefix, or the default prefix if no record is found.</summary>
        public string GetGuildPrefix(ulong guildId)
        {
            if (_cachedPrefixes.TryGetValue(guildId, out string prefix)) return prefix;

            using (var db = MakeDbContext())
            {
                prefix = db.Prefixes.Find(guildId)?.Prefix ?? DefaultPrefix;
            }

            _cachedPrefixes.TryAdd(guildId, prefix);
            return prefix;
        }

        /// <summary>Retrieves the specified guild's custom prefix, or the default prefix if no record is found.
        /// Provides the benefit of an asynchronous database access if one is necessary.</summary>
        public async ValueTask<string> GetGuildPrefixAsync(DiscordGuild guild)
        {
            if (guild == null) return DefaultPrefix;
            if (_cachedPrefixes.TryGetValue(guild.Id, out string prefix)) return prefix;

            using (var db = MakeDbContext())
            {
                prefix = (await db.Prefixes.FindAsync(guild.Id))?.Prefix ?? DefaultPrefix;
            }

            _cachedPrefixes.TryAdd(guild.Id, prefix);
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
                _cachedPrefixes[guildId] = prefix;
            }
        }


        /// <summary>Whether the specified context requires a prefix for commands.</summary>
        public bool RequiresPrefix(CommandContext context) => RequiresPrefix(context?.Channel);

        /// <summary>Whether the specified channel requires a prefix for commands.</summary>
        public bool RequiresPrefix(DiscordChannel channel)
        {
            if (channel == null) return false;
            if (_cachedNeedsPrefix.TryGetValue(channel.Id, out bool needs)) return needs;

            using (var db = MakeDbContext())
            {
                needs = channel.Guild != null && db.NoPrefixGuildChannels.Find(channel.Id) == null;
            }

            _cachedNeedsPrefix.TryAdd(channel.Id, needs);
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
                _cachedNeedsPrefix[channelId] = nowNeeds;
                return nowNeeds;
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

            _log.Debug($"New scoreboard entry: {entry}");
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
                _log.Debug($"Grabbed {list.Count} score entries");
                return list;
            }
        }
    }
}
