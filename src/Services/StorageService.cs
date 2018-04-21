using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
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

        public readonly string defaultPrefix;
        public Dictionary<ulong, string> prefixes;
        public List<GameInstance> gameInstances;
        public List<ScoreEntry> scoreEntries;
        public string wakaExclude = "";


        public StorageService(DiscordSocketClient client, LoggingService logger, IConfigurationRoot config)
        {
            this.client = client;
            this.logger = logger;

            defaultPrefix = config["prefix"];
            LoadWakaExclude();
            LoadPrefixes();
            LoadScoreboard();
            LoadGames();
        }


        public string GetPrefix(ulong serverId)
        {
            return (prefixes.ContainsKey(serverId)) ? prefixes[serverId] : defaultPrefix;
        }
        public string GetPrefix(SocketGuild guild = null)
        {
            return (guild == null) ? defaultPrefix : GetPrefix(guild.Id);
        }


        public string GetPrefixOrEmpty(SocketGuild guild)
        {
            return (guild == null) ? "" : GetPrefix(guild.Id);
        }


        public void SetPrefix(ulong guildId, string prefix)
        {
            if (prefixes.ContainsKey(guildId))
            {
                string replace = "";

                if (prefix == defaultPrefix)
                {
                    prefixes.Remove(guildId);
                }
                else
                {
                    prefixes[guildId] = prefix;
                    replace = Regex.Escape($"{guildId} {prefix}");
                }

                File.WriteAllText(BotFile.Prefixes, Regex.Replace(File.ReadAllText(BotFile.Prefixes), $@"{guildId} .*", replace));
            }
            else if (prefix != defaultPrefix)
            {
                prefixes.Add(guildId, prefix);
                File.AppendAllText(BotFile.Prefixes, $"\n{guildId} {prefix}");
            }
        }


        public bool ToggleWaka(ulong guildId)
        {
            bool nowaka = wakaExclude.Contains($"{guildId}");
            if (nowaka)
            {
                wakaExclude = wakaExclude.Replace($"{guildId} ", "");
                File.WriteAllText(BotFile.WakaExclude, wakaExclude);
            }
            else
            {
                wakaExclude += $"{guildId} ";
                File.AppendAllText(BotFile.WakaExclude, $"{guildId} ");
            }

            return nowaka;
        }


        public void DeleteGame(int i)
        {
            logger.Log(LogSeverity.Info, $"Removing game at {gameInstances[i].channelId}");
            if (File.Exists(gameInstances[i].GameFile)) File.Delete(gameInstances[i].GameFile);
            gameInstances.RemoveAt(i);
        }


        public void AddScore(ScoreEntry entry)
        {
            string scoreString = $"{entry.state} {entry.score} {entry.turns} {entry.userId} \"{entry.username.Replace("\"", "")}\" \"{entry.date}\" \"{entry.channel.Replace("\"", "")}\"";
            File.AppendAllText(BotFile.Scoreboard, $"\n{scoreString}");
            scoreEntries.Add(entry);
            logger.Log(LogSeverity.Verbose, $"New scoreboard entry: {scoreString}");
        }


        public void SortScores()
        {
            //scoreEntries.Sort((a, b) => b.score.CompareTo(a.score));
            scoreEntries = scoreEntries.OrderByDescending(x => x.score).ToList();
        }



        private void LoadWakaExclude()
        {
            wakaExclude = File.Exists(BotFile.WakaExclude) ? File.ReadAllText(BotFile.WakaExclude) : "";
        }


        private void LoadScoreboard()
        {
            scoreEntries = new List<ScoreEntry>();
            uint fail = 0;

            string[] lines = File.ReadAllLines(BotFile.Scoreboard);
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].StartsWith('#')) continue;

                //Deconstruct line
                var splice = new List<string>(lines[i].Split(' ', 5)); // state, score, turns, userId, (rest)
                if (splice.Count == 5)
                {
                    splice.AddRange(splice[4].Split('"').Where(x => !string.IsNullOrWhiteSpace(x))); // username, date, guild
                    splice.RemoveAt(4);
                }

                if (splice.Count == 7
                    && Enum.TryParse(splice[0], out GameInstance.State state)
                    && int.TryParse(splice[1], out int score)
                    && int.TryParse(splice[2], out int turns)
                    && ulong.TryParse(splice[3], out ulong userId))
                {
                    scoreEntries.Add(new ScoreEntry(state, score, turns, userId, splice[4], splice[5], splice[6]));
                }
                else
                {
                    logger.Log(LogSeverity.Error, $"Invalid entry in {BotFile.Scoreboard} at line {i}");
                    fail++;
                }
            }

            SortScores();
            logger.Log(LogSeverity.Info, $"Loaded {scoreEntries.Count} scoreboard entries from {BotFile.Scoreboard}{$" with {fail} errors".If(fail > 0)}.");
        }


        private void LoadPrefixes()
        {
            prefixes = new Dictionary<ulong, string>();
            uint fail = 0;

            string[] lines = File.ReadAllLines(BotFile.Prefixes);
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].StartsWith('#')) continue;
                string[] data = lines[i].Split(' '); // Splits into guild ID and prefix

                if (data.Length == 2 && ulong.TryParse(data[0], out ulong ID))
                {
                    string prefix = data[1];
                    prefixes.Add(ID, prefix);
                }
                else
                {
                    logger.Log(LogSeverity.Error, $"Invalid entry in {BotFile.Prefixes} at line {i}");
                    fail++;
                }
            }

            logger.Log(LogSeverity.Info, $"Loaded {prefixes.Count} custom prefixes from {BotFile.Prefixes}{$" with {fail} errors".If(fail > 0)}.");
        }


        private void LoadGames()
        {
            if (Directory.Exists(GameInstance.Folder))
            {
                gameInstances = new List<GameInstance>();
                uint fail = 0;

                string[] files = Directory.GetFiles(GameInstance.Folder);
                for (int i = 0; i < files.Length; i++)
                {
                    try
                    {
                        ulong channelId = ulong.Parse(files[i].Replace(GameInstance.Folder, "").Replace(GameInstance.Extension, ""));
                        GameInstance game = new GameInstance(channelId, 1, null, client, this, logger);
                        game.LoadFromFile();
                        gameInstances.Add(game);
                    }
                    catch (Exception e)
                    {
                        logger.Log(LogSeverity.Error, $"Couldn't load game {files[i]}: {e.Message}");
                        fail++;
                    }
                }

                logger.Log(LogSeverity.Info, $"Loaded {gameInstances.Count} games from previous session{$" with {fail} errors".If(fail > 0)}.");
            }
            else
            {
                Directory.CreateDirectory(GameInstance.Folder);
                logger.Log(LogSeverity.Warning, $"Created missing directory \"{GameInstance.Folder}\"");
            }
        }
    }
}
