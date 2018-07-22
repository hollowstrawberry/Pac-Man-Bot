using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Discord;
using Discord.Rest;
using Discord.WebSocket;
using PacManBot.Utils;
using PacManBot.Constants;
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

        public readonly PacManDbContext db;
        private readonly ConcurrentDictionary<ulong, string> cachedPrefixes;
        private readonly ConcurrentDictionary<ulong, bool> cachedAllowsAutoresponse;
        private readonly ConcurrentDictionary<ulong, bool> cachedNeedsPrefix;

        public string DefaultPrefix { get; }
        public RestApplication AppInfo { get; private set; }



        public StorageService(BotConfig config, DiscordShardedClient client, LoggingService logger)
        {
            this.client = client;
            this.logger = logger;

            db = new PacManDbContext(config.dbConnectionString);
            db.Database.EnsureCreated();

            DefaultPrefix = config.defaultPrefix;

            cachedPrefixes = new ConcurrentDictionary<ulong, string>();
            cachedAllowsAutoresponse = new ConcurrentDictionary<ulong, bool>();
            cachedNeedsPrefix = new ConcurrentDictionary<ulong, bool>();

            client.LoggedIn += LoadAppInfo;
        }




        /// <summary>Retrieves the specified guild's active prefix if the guild exists, or the default prefix otherwise.</summary>
        public string GetPrefix(ulong guildId)
        {
            if (cachedPrefixes.TryGetValue(guildId, out string prefix)) return prefix;

            prefix = db.Prefixes.Find(guildId)?.Prefix ?? DefaultPrefix;

            cachedPrefixes.TryAdd(guildId, prefix);
            return prefix;
        }

        /// <summary>Retrieves the specified guild's active prefix if the guild exists, or the default prefix otherwise.</summary>
        public string GetPrefix(IGuild guild = null) => guild == null ? DefaultPrefix : GetPrefix(guild.Id);

        /// <summary>Retrieves the specified guild's active prefix if the guild exists, or an empty string otherwise.</summary>
        public string GetPrefixOrEmpty(IGuild guild) => guild == null ? "" : GetPrefix(guild.Id);


        /// <summary>Changes the prefix of the specified guild.</summary>
        public void SetPrefix(ulong guildId, string prefix)
        {
            string old = cachedPrefixes[guildId];

            if (prefix == DefaultPrefix) db.Prefixes.Remove((guildId, prefix));
            else
            {
                if (old == DefaultPrefix) db.Prefixes.Add((guildId, prefix));
                else db.Prefixes.Find(guildId).Prefix = prefix;
            }

            db.SaveChanges();
            cachedPrefixes[guildId] = prefix;
        }




        /// <summary>Whether the specified guild is set to allow message autoresponses.</summary>
        public bool AllowsAutoresponse(ulong guildId)
        {
            if (cachedAllowsAutoresponse.TryGetValue(guildId, out bool allows)) return allows;

            allows = !db.NoAutoresponseGuilds.Contains((NoAutoresponseGuild)guildId);
            cachedAllowsAutoresponse.TryAdd(guildId, allows);

            return allows;
        }


        /// <summary>Toggles message autoresponses on or off in the specified guild and returns the new value.</summary>
        public bool ToggleAutoresponse(ulong guildId)
        {
            bool allows = cachedAllowsAutoresponse[guildId];

            if (allows) db.NoAutoresponseGuilds.Add(guildId);
            else db.NoAutoresponseGuilds.Remove(guildId);

            db.SaveChanges();
            cachedAllowsAutoresponse[guildId] = !allows;
            return !allows;
        }




        /// <summary>Whether the specified channel is set to not require a prefix for commands.</summary>
        public bool NeedsPrefix(ulong channelId)
        {
            if (cachedNeedsPrefix.TryGetValue(channelId, out bool needs)) return needs;

            needs = !db.NoPrefixChannels.Contains((NoPrefixChannel)channelId);
            cachedNeedsPrefix.TryAdd(channelId, needs);

            return needs;
        }


        /// <summary>Toggles the specified channel between requiring a prefix for commands and not.</summary>
        public bool ToggleNoPrefix(ulong channelId)
        {
            bool needsPrefix = cachedNeedsPrefix[channelId];

            if (needsPrefix) db.NoPrefixChannels.Add(channelId);
            else db.NoPrefixChannels.Remove(channelId);

            db.SaveChanges();
            cachedNeedsPrefix[channelId] = !needsPrefix;
            return !needsPrefix;
        }




        /// <summary>Adds a new entry to the <see cref="Games.Concrete.PacManGame"/> scoreboard.</summary>
        public void AddScore(ScoreEntry entry)
        {
            logger.Log(LogSeverity.Info, LogSource.Storage, $"New scoreboard entry: {entry}");

            db.PacManScores.Add(entry);
            db.SaveChanges();
        }


        /// <summary>Retrieves a list of scores from the database that fulfills the specified requirements.</summary>
        public List<ScoreEntry> GetScores(TimePeriod period, int amount = 1, int start = 0, ulong? userId = null)
        {
            var scores = db.PacManScores.AsQueryable();

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




        private async Task LoadAppInfo()
        {
            AppInfo = await client.GetApplicationInfoAsync(Bot.DefaultOptions);
            client.LoggedIn -= LoadAppInfo;
        }
    }
}
