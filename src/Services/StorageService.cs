using System;
using System.IO;
using Microsoft.Data.Sqlite;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Discord;
using Discord.Rest;
using Discord.WebSocket;
using PacManBot.Games;
using PacManBot.Utils;
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

        private readonly ConcurrentDictionary<ulong, string> cachedPrefixes;
        private readonly ConcurrentDictionary<ulong, bool> cachedAllowsAutoresponse;
        private readonly ConcurrentDictionary<ulong, bool> cachedNoPrefixChannel;

        public string DefaultPrefix { get; }
        public RestApplication AppInfo { get; private set; }


        public SqliteConnection NewDatabaseConnection() => new SqliteConnection($"Data Source={Files.Database};");



        public StorageService(DiscordShardedClient client, LoggingService logger, BotConfig config)
        {
            this.client = client;
            this.logger = logger;

            DefaultPrefix = config.defaultPrefix;
            cachedPrefixes = new ConcurrentDictionary<ulong, string>();
            cachedAllowsAutoresponse = new ConcurrentDictionary<ulong, bool>();
            cachedNoPrefixChannel = new ConcurrentDictionary<ulong, bool>();

            SetupDatabase();

            client.LoggedIn += LoadAppInfo;
        }




        /// <summary>Retrieves the specified guild's active prefix if the guild exists, or the default prefix otherwise.</summary>
        public string GetPrefix(ulong guildId)
        {
            if (cachedPrefixes.TryGetValue(guildId, out string prefix)) return prefix;

            using (var connection = NewDatabaseConnection())
            {
                connection.Open();
                var command = new SqliteCommand($"SELECT prefix FROM prefixes WHERE id=@id LIMIT 1", connection)
                    .WithParameter("@id", guildId);

                prefix = (string)command.ExecuteScalar() ?? DefaultPrefix;
            }

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
            cachedPrefixes[guildId] = prefix;

            string sql = "DELETE FROM prefixes WHERE id=@id;";
            if (prefix != DefaultPrefix) sql += "INSERT INTO prefixes VALUES (@id, @prefix);";

            using (var connection = NewDatabaseConnection())
            {
                connection.Open();

                new SqliteCommand(sql, connection)
                    .WithParameter("@id", guildId)
                    .WithParameter("@prefix", prefix)
                    .ExecuteNonQuery();
            }
        }




        /// <summary>Whether the specified guild is set to allow message autoresponses.</summary>
        public bool AllowsAutoresponse(ulong guildId)
        {
            if (cachedAllowsAutoresponse.TryGetValue(guildId, out bool allows)) return allows;

            using (var connection = NewDatabaseConnection())
            {
                connection.Open();

                var command = new SqliteCommand("SELECT * FROM noautoresponse WHERE id=@id LIMIT 1", connection)
                    .WithParameter("@id", guildId);
                allows = command.ExecuteScalar() == null;
                cachedAllowsAutoresponse.TryAdd(guildId, allows);
                return allows;
            }
        }


        /// <summary>Toggles message autoresponses on or off in the specified guild.</summary>
        public bool ToggleAutoresponse(ulong guildId)
        {
            cachedAllowsAutoresponse[guildId] = !cachedAllowsAutoresponse[guildId];

            using (var connection = NewDatabaseConnection())
            {
                connection.Open();
                new SqliteCommand("BEGIN", connection).ExecuteNonQuery();

                int rows = new SqliteCommand("DELETE FROM noautoresponse WHERE id=@id", connection)
                    .WithParameter("@id", guildId)
                    .ExecuteNonQuery();

                if (rows == 0)
                {
                    new SqliteCommand("INSERT INTO noautoresponse VALUES (@id)", connection)
                        .WithParameter("@id", guildId)
                        .ExecuteNonQuery();
                }

                new SqliteCommand("END", connection).ExecuteNonQuery();
                return rows != 0;
            }
        }




        /// <summary>Whether the specified channel is set to not require a prefix for commands.</summary>
        public bool NoPrefixChannel(ulong channelId)
        {
            if (cachedNoPrefixChannel.TryGetValue(channelId, out bool noPrefixMode)) return noPrefixMode;

            using (var connection = NewDatabaseConnection())
            {
                connection.Open();

                var command = new SqliteCommand("SELECT * FROM noprefix WHERE id=@id LIMIT 1", connection)
                    .WithParameter("@id", channelId);
                noPrefixMode = command.ExecuteScalar() != null;
                cachedNoPrefixChannel.TryAdd(channelId, noPrefixMode);
                return noPrefixMode;
            }
        }


        /// <summary>Toggles the specified channel between requiring a prefix for commands and not.</summary>
        public bool ToggleNoPrefix(ulong channelId)
        {
            cachedNoPrefixChannel[channelId] = !cachedNoPrefixChannel[channelId];

            using (var connection = NewDatabaseConnection())
            {
                connection.Open();
                new SqliteCommand("BEGIN", connection).ExecuteNonQuery();

                int rows = new SqliteCommand("DELETE FROM noprefix WHERE id=@id", connection)
                    .WithParameter("@id", channelId)
                    .ExecuteNonQuery();

                if (rows == 0)
                {
                    new SqliteCommand("INSERT INTO noprefix VALUES (@id)", connection)
                        .WithParameter("@id", channelId)
                        .ExecuteNonQuery();
                }

                new SqliteCommand("END", connection).ExecuteNonQuery();
                return rows != 0;
            }
        }




        /// <summary>Adds a new entry to the <see cref="PacManGame"/> scoreboard.</summary>
        public void AddScore(ScoreEntry entry)
        {
            logger.Log(LogSeverity.Info, LogSource.Storage, $"New scoreboard entry: {entry}");

            using (var connection = NewDatabaseConnection())
            {
                connection.Open();

                string sql = "INSERT INTO scoreboard VALUES (@score, @userid, @state, @turns, @username, @channel, @date)";
                new SqliteCommand(sql, connection)
                    .WithParameter("@score", entry.score)
                    .WithParameter("@userid", entry.userId)
                    .WithParameter("@state", entry.state)
                    .WithParameter("@turns", entry.turns)
                    .WithParameter("@username", entry.username)
                    .WithParameter("@channel", entry.channel)
                    .WithParameter("@date", entry.date)
                    .ExecuteNonQuery();
            }
        }


        /// <summary>Retrieves a list of scores from the database that fulfills the specified requirements.</summary>
        public List<ScoreEntry> GetScores(TimePeriod period, int amount = 1, int start = 0, ulong? userId = null)
        {
            var conditions = new List<string>();
            if (period != TimePeriod.All) conditions.Add($"date>=@date");
            if (userId != null) conditions.Add($"userid=@userid");

            string sql = "SELECT * FROM scoreboard " +
             (conditions.Count == 0 ? "" : $"WHERE {string.Join(" AND ", conditions)} ") +
             "ORDER BY score DESC LIMIT @amount OFFSET @start";

            var scores = new List<ScoreEntry>();

            using (var connection = NewDatabaseConnection())
            {
                connection.Open();

                var command = new SqliteCommand(sql, connection)
                    .WithParameter("@amount", amount)
                    .WithParameter("@start", start)
                    .WithParameter("@userid", userId)
                    .WithParameter("@date", DateTime.Now - TimeSpan.FromHours((int)period));

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        int score = reader.GetInt32(0);
                        ulong id = (ulong)reader.GetInt64(1);
                        State state = (State)reader.GetInt32(2);
                        int turns = reader.GetInt32(3);
                        string name = reader.GetString(4);
                        string channel = reader.GetString(5);
                        DateTime date = reader.GetDateTime(6);

                        scores.Add(new ScoreEntry(score, id, state, turns, name, channel, date));
                    }
                }
            }

            logger.Log(LogSeverity.Info, LogSource.Storage, $"Grabbed {scores.Count} score entries");
            return scores;
        }




        private void SetupDatabase()
        {
            if (!File.Exists(Files.Database))
            {
                File.Create(Files.Database);
                logger.Log(LogSeverity.Info, LogSource.Storage, "Creating database");
            }

            using (var connection = NewDatabaseConnection())
            {
                connection.Open();

                foreach (string table in new[] {
                    "prefixes (id BIGINT PRIMARY KEY, prefix TEXT)",
                    "scoreboard (score INT, userid BIGINT, state INT, turns INT, username TEXT, channel TEXT, date DATETIME)",
                    "noautoresponse (id BIGINT PRIMARY KEY)",
                    "noprefix (id BIGINT PRIMARY KEY)"
                })
                {
                    new SqliteCommand($"CREATE TABLE IF NOT EXISTS {table}", connection).ExecuteNonQuery();
                }
            }
        }


        private async Task LoadAppInfo()
        {
            AppInfo = await client.GetApplicationInfoAsync(Bot.DefaultOptions);
            client.LoggedIn -= LoadAppInfo;
        }
    }
}
