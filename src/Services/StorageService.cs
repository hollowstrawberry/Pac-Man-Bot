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
using PacManBot.Constants;
using PacManBot.Modules.PacMan;

namespace PacManBot.Services
{
    public class StorageService
    {
        private readonly DiscordSocketClient client;
        private readonly LoggingService logger;

        private JsonSerializerSettings GameJsonSettings = new JsonSerializerSettings
        {
            ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor
        };

        public IConfigurationRoot BotContent { get; private set; }
        public string DefaultPrefix { get; }
        public Dictionary<ulong, string> Prefixes { get; private set; }
        public List<GameInstance> GameInstances { get; private set; }
        public List<ScoreEntry> ScoreEntries { get; private set; }
        public string WakaExclude { get; private set; }




        public StorageService(DiscordSocketClient client, LoggingService logger, BotConfig config)
        {
            this.client = client;
            this.logger = logger;

            DefaultPrefix = config.defaultPrefix;
            LoadBotContent();
            LoadWakaExclude();
            LoadPrefixes();
            LoadScoreboard();
            LoadGames();
        }



        public string GetPrefix(ulong serverId)
        {
            return (Prefixes.ContainsKey(serverId)) ? Prefixes[serverId] : DefaultPrefix;
        }
        public string GetPrefix(SocketGuild guild = null)
        {
            return (guild == null) ? DefaultPrefix : GetPrefix(guild.Id);
        }


        public string GetPrefixOrEmpty(SocketGuild guild)
        {
            return (guild == null) ? "" : GetPrefix(guild.Id);
        }


        public void SetPrefix(ulong guildId, string prefix)
        {
            if (Prefixes.ContainsKey(guildId))
            {
                string replace = "";

                if (prefix == DefaultPrefix)
                {
                    Prefixes.Remove(guildId);
                }
                else
                {
                    Prefixes[guildId] = prefix;
                    replace = $"{guildId} {Regex.Escape(prefix)}\n";
                }

                File.WriteAllText(BotFile.Prefixes, Regex.Replace(File.ReadAllText(BotFile.Prefixes), $@"{guildId}.*\n", replace));
            }
            else if (prefix != DefaultPrefix)
            {
                Prefixes.Add(guildId, prefix);
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


        public void DeleteGame(int i)
        {
            if (i < GameInstances.Count())
            {
                logger.Log(LogSeverity.Verbose, LogSource.Storage, $"Removing game at {GameInstances[i].channelId}");
                if (File.Exists(GameInstances[i].GameFile)) File.Delete(GameInstances[i].GameFile);
                GameInstances.RemoveAt(i);
            }
            else
            {
                logger.Log(LogSeverity.Error, LogSource.Storage, $"Trying to delete game at index {i}: Not found.");
            }
        }
        public void DeleteGame(GameInstance game)
        {
            if (GameInstances.Contains(game)) DeleteGame(GameInstances.IndexOf(game));
            else logger.Log(LogSeverity.Error, LogSource.Storage, $"Trying to delete game: Not found.");
        }


        public void StoreGame(GameInstance game)
        {
            File.WriteAllText(game.GameFile, JsonConvert.SerializeObject(game), Encoding.UTF8);
        }


        public List<ScoreEntry> GetScores(Utils.TimePeriod period)
        {
            if (period == Utils.TimePeriod.all) return ScoreEntries;

            var date = DateTime.Now;
            return ScoreEntries.Where(s => (date - s.date).TotalHours <= (int)period).ToList();
        }


        public void AddScore(ScoreEntry entry)
        {
            string scoreString = entry.ToString();
            logger.Log(LogSeverity.Verbose, LogSource.Storage, $"New scoreboard entry: {scoreString}");
            File.AppendAllText(BotFile.Scoreboard, $"\n{scoreString}");

            int index = ScoreEntries.BinarySearch(entry, ScoreEntry.Comparer);
            if (index < 0) index = ~index;
            ScoreEntries.Insert(index, entry); //Adds entry in sorted position
        }




        public void LoadBotContent()
        {
            BotContent = new ConfigurationBuilder().SetBasePath(AppContext.BaseDirectory).AddJsonFile(BotFile.Contents).Build();
        }


        private void LoadWakaExclude()
        {
            WakaExclude = File.Exists(BotFile.WakaExclude) ? File.ReadAllText(BotFile.WakaExclude) : "";
        }


        private void LoadPrefixes()
        {
            Prefixes = new Dictionary<ulong, string>();
            uint fail = 0;

            string[] lines = File.ReadAllLines(BotFile.Prefixes);
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].StartsWith('#') || string.IsNullOrWhiteSpace(lines[i])) continue;
                string[] data = lines[i].Split(' '); // Splits into guild ID and prefix

                if (data.Length == 2 && ulong.TryParse(data[0], out ulong ID))
                {
                    string prefix = data[1];
                    Prefixes.Add(ID, prefix);
                }
                else
                {
                    logger.Log(LogSeverity.Error, LogSource.Storage, $"Invalid entry in {BotFile.Prefixes} at line {i}");
                    fail++;
                }
            }

            logger.Log(LogSeverity.Info, LogSource.Storage, $"Loaded {Prefixes.Count} custom prefixes from {BotFile.Prefixes}{$" with {fail} errors".If(fail > 0)}.");
        }


        private void LoadScoreboard()
        {
            ScoreEntries = new List<ScoreEntry>();
            uint fail = 0;

            string[] lines = File.ReadAllLines(BotFile.Scoreboard);
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].StartsWith('#')) continue;

                if (ScoreEntry.TryParse(lines[i], out ScoreEntry newEntry))
                {
                    ScoreEntries.Add(newEntry);
                }
                else
                {
                    logger.Log(LogSeverity.Error, LogSource.Storage, $"Invalid entry in {BotFile.Scoreboard} at line {i}");
                    fail++;
                }
            }

            ScoreEntries.Sort(ScoreEntry.Comparer); // The list will stay sorted as new elements will be added in sorted position
            logger.Log(LogSeverity.Info, LogSource.Storage, $"Loaded {ScoreEntries.Count} scoreboard entries from {BotFile.Scoreboard}{$" with {fail} errors".If(fail > 0)}.");
        }


        private void LoadGames()
        {
            GameInstances = new List<GameInstance>();

            if (Directory.Exists(GameInstance.Folder))
            {
                uint fail = 0;

                foreach (string file in Directory.GetFiles(GameInstance.Folder))
                {
                    if (file.EndsWith(GameInstance.Extension))
                    {
                        try
                        {
                            var game = JsonConvert.DeserializeObject<GameInstance>(File.ReadAllText(file), GameJsonSettings);
                            game.SetServices(client, this, logger);
                            GameInstances.Add(game);
                        }
                        catch (Exception e)
                        {
                            logger.Log(LogSeverity.Error, LogSource.Storage, $"Couldn't load game at {file}: {e.Message}");
                            fail++;
                        }
                    }
                }

                logger.Log(LogSeverity.Info, LogSource.Storage, $"Loaded {GameInstances.Count} games from previous session{$" with {fail} errors".If(fail > 0)}.");
            }
            else
            {
                Directory.CreateDirectory(GameInstance.Folder);
                logger.Log(LogSeverity.Warning, LogSource.Storage, $"Created missing directory \"{GameInstance.Folder}\"");
            }
        }
    }
}
