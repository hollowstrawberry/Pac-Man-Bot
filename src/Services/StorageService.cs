using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using Newtonsoft.Json;
using Microsoft.Extensions.Configuration;
using Discord;
using Discord.WebSocket;
using PacManBot.Games;
using PacManBot.Constants;

namespace PacManBot.Services
{
    public class StorageService
    {
        private readonly DiscordShardedClient client;
        private readonly LoggingService logger;
        private readonly Dictionary<ulong, string> prefixes;
        private readonly List<ScoreEntry> scoreEntries;
        private readonly List<IChannelGame> games; // Channel-specific games
        private readonly List<IBaseGame> transientGames; // Non-channel specific games
        private readonly JsonSerializerSettings gameJsonSettings = new JsonSerializerSettings
        {
            ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor,
        };

        public IApplication AppInfo { get; set; }
        public string DefaultPrefix { get; private set; }
        public string WakaExclude { get; private set; }
        public IReadOnlyList<IChannelGame> Games { get; private set; }
        public IReadOnlyList<IBaseGame> TransientGames { get; private set; }
        public IConfigurationRoot BotContent { get; private set; }
        public string[] PettingMessages { get; private set; }


        public StorageService(DiscordShardedClient client, LoggingService logger, BotConfig config)
        {
            this.client = client;
            this.logger = logger;

            AppInfo = null;
            DefaultPrefix = config.defaultPrefix;
            prefixes = new Dictionary<ulong, string>();
            scoreEntries = new List<ScoreEntry>();
            games = new List<IChannelGame>();
            transientGames = new List<IBaseGame>();

            Games = games.AsReadOnly();
            TransientGames = transientGames.AsReadOnly();

            LoadBotContent();
            LoadWakaExclude();
            LoadPrefixes();
            LoadScoreboard();
            LoadGames();
        }



        public string GetPrefix(ulong serverId)
        {
            return (prefixes.ContainsKey(serverId)) ? prefixes[serverId] : DefaultPrefix;
        }
        public string GetPrefix(IGuild guild = null)
        {
            return (guild == null) ? DefaultPrefix : GetPrefix(guild.Id);
        }


        public string GetPrefixOrEmpty(IGuild guild)
        {
            return (guild == null) ? "" : GetPrefix(guild.Id);
        }


        public void SetPrefix(ulong guildId, string prefix)
        {
            if (prefixes.ContainsKey(guildId))
            {
                string replace = "";

                if (prefix == DefaultPrefix)
                {
                    prefixes.Remove(guildId);
                }
                else
                {
                    prefixes[guildId] = prefix;
                    replace = $"{guildId} {Regex.Escape(prefix)}\n";
                }

                File.WriteAllText(BotFile.Prefixes, Regex.Replace(File.ReadAllText(BotFile.Prefixes), $@"{guildId}.*\n", replace));
            }
            else if (prefix != DefaultPrefix)
            {
                prefixes.Add(guildId, prefix);
                File.AppendAllText(BotFile.Prefixes, $"{guildId} {prefix}\n");
            }
        }


        public bool ToggleWaka(ulong guildId)
        {
            bool nowaka = WakaExclude.Contains($"{guildId}");
            if (nowaka)
            {
                WakaExclude = WakaExclude.Replace($"{guildId} ", "");
                File.WriteAllText(BotFile.WakaExclude, WakaExclude);
            }
            else
            {
                WakaExclude += $"{guildId} ";
                File.AppendAllText(BotFile.WakaExclude, $"{guildId} ");
            }

            return nowaka;
        }


        public void AddGame(IChannelGame game)
        {
            games.Add(game);
        }


        public void AddTransientGame(IBaseGame game)
        {
            transientGames.Add(game);
        }


        public void DeleteGame(IChannelGame game)
        {
            try
            {
                game.CancelRequests();
                if (game is IStoreableGame sGame && File.Exists(sGame.GameFile)) File.Delete(sGame.GameFile);
                games.Remove(game);
                logger.Log(LogSeverity.Verbose, LogSource.Storage, $"Removed {game.GetType().Name} at {game.ChannelId}");
            }
            catch (Exception e)
            {
                logger.Log(LogSeverity.Error, LogSource.Storage, $"Trying to remove game at {game.ChannelId}: {e.Message}");
            }
        }


        public void DeleteGame(IBaseGame game)
        {
            try
            {
                game.CancelRequests();
                transientGames.Remove(game);
                logger.Log(LogSeverity.Verbose, LogSource.Storage, $"Removed {game.GetType().Name} for {game.UserId[0]}");
            }
            catch (Exception e)
            {
                logger.Log(LogSeverity.Error, LogSource.Storage, $"Trying to remove transient game for user {game.UserId[0]}: {e.Message}");
            }
        }


        public IChannelGame GetGame(ulong channelId)
        {
            return games.FirstOrDefault(g => g.ChannelId == channelId);
        }

        public T GetGame<T>(ulong channelId) where T : IChannelGame
        {
            return (T)games.FirstOrDefault(g => g.ChannelId == channelId && g is T);
        }


        public IBaseGame GetTransientGame(ulong channelId)
        {
            return games.FirstOrDefault(g => g.ChannelId == channelId);
        }

        public T GetTransientGame<T>(ulong userId) where T : IBaseGame
        {
            return (T)transientGames.FirstOrDefault(g => g.UserId.Contains(userId) && g is T);
        }


        public void StoreGame(IStoreableGame game)
        {
            File.WriteAllText(game.GameFile, JsonConvert.SerializeObject(game), Encoding.UTF8);
        }


        public IReadOnlyList<ScoreEntry> GetScores(Utils.TimePeriod period)
        {
            if (period == Utils.TimePeriod.all) return scoreEntries.AsReadOnly();

            var date = DateTime.Now;
            return scoreEntries.Where(s => (date - s.date).TotalHours <= (int)period).ToList().AsReadOnly();
        }


        public void AddScore(ScoreEntry entry)
        {
            string scoreString = entry.ToString();
            logger.Log(LogSeverity.Info, LogSource.Storage, $"New scoreboard entry: {scoreString}");
            File.AppendAllText(BotFile.Scoreboard, $"\n{scoreString}");

            int index = scoreEntries.BinarySearch(entry, ScoreEntry.Comparer);
            if (index < 0) index = ~index;
            scoreEntries.Insert(index, entry); //Adds entry in sorted position
        }




        public void LoadBotContent()
        {
            BotContent = new ConfigurationBuilder().SetBasePath(AppContext.BaseDirectory).AddJsonFile(BotFile.Contents).Build();
            PettingMessages = BotContent["petting"].Split('\n', StringSplitOptions.RemoveEmptyEntries);
            logger.LoadLogExclude(this);
        }


        private void LoadWakaExclude()
        {
            WakaExclude = File.Exists(BotFile.WakaExclude) ? File.ReadAllText(BotFile.WakaExclude) : "";
        }


        private void LoadPrefixes()
        {
            uint fail = 0;

            string[] lines = File.ReadAllLines(BotFile.Prefixes);
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].StartsWith('#') || string.IsNullOrWhiteSpace(lines[i])) continue;
                string[] data = lines[i].Split(' '); // Splits into guild ID and prefix

                if (data.Length == 2 && ulong.TryParse(data[0], out ulong ID))
                {
                    string prefix = data[1];
                    prefixes.Add(ID, prefix);
                }
                else
                {
                    logger.Log(LogSeverity.Error, LogSource.Storage, $"Invalid entry in {BotFile.Prefixes} at line {i}");
                    fail++;
                }
            }

            logger.Log(LogSeverity.Info, LogSource.Storage, $"Loaded {prefixes.Count} custom prefixes from {BotFile.Prefixes}{$" with {fail} errors".If(fail > 0)}.");
        }


        private void LoadScoreboard()
        {
            uint fail = 0;

            string[] lines = File.ReadAllLines(BotFile.Scoreboard);
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].StartsWith('#')) continue;

                if (ScoreEntry.TryParse(lines[i], out ScoreEntry newEntry))
                {
                    scoreEntries.Add(newEntry);
                }
                else
                {
                    logger.Log(LogSeverity.Error, LogSource.Storage, $"Invalid entry in {BotFile.Scoreboard} at line {i}");
                    fail++;
                }
            }

            scoreEntries.Sort(ScoreEntry.Comparer); // The list will stay sorted as new elements will be added in sorted position
            logger.Log(LogSeverity.Info, LogSource.Storage, $"Loaded {scoreEntries.Count} scoreboard entries from {BotFile.Scoreboard}{$" with {fail} errors".If(fail > 0)}.");
        }


        private void LoadGames()
        {
            if (!Directory.Exists(GameUtils.GameFolder))
            {
                Directory.CreateDirectory(GameUtils.GameFolder);
                logger.Log(LogSeverity.Warning, LogSource.Storage, $"Created missing directory \"{GameUtils.GameFolder}\"");
                return;
            }

            uint fail = 0;
            bool firstFail = true;

            foreach (string file in Directory.GetFiles(GameUtils.GameFolder))
            {
                if (file.EndsWith(GameUtils.GameExtension))
                {
                    try
                    {
                        IStoreableGame game;
                        if (file.Contains("pet"))
                        {
                            game = JsonConvert.DeserializeObject<PetGame>(File.ReadAllText(file), gameJsonSettings);
                            transientGames.Add(game);
                        }
                        else
                        {
                            game = JsonConvert.DeserializeObject<PacManGame>(File.ReadAllText(file), gameJsonSettings);
                            games.Add(game as PacManGame);
                        }
                        game.SetServices(client, logger, this);
                        // StoreGame(game); // Update old files
                    }
                    catch (Exception e)
                    {
                        logger.Log(LogSeverity.Error, LogSource.Storage, $"Couldn't load game at {file}: {(firstFail ? e.ToString() : e.Message)}");
                        Console.ReadLine();
                        fail++;
                        firstFail = false;
                    }
                }
            }

            logger.Log(LogSeverity.Info, LogSource.Storage, $"Loaded {games.Count} games from previous session{$" with {fail} errors".If(fail > 0)}.");
        }
    }
}
