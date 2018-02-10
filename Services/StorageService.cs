using System;
using System.IO;
using System.Collections.Generic;
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
        public List<PacManGame> gameInstances;

        public StorageService(DiscordSocketClient client, LoggingService logger, IConfigurationRoot config)
        {
            this.client = client;
            this.logger = logger;

            defaultPrefix = config["prefix"];
            LoadPrefixes();
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


        private void LoadPrefixes()
        {
            prefixes = new Dictionary<ulong, string>(); 

            string[] line = File.ReadAllLines(BotFile.Prefixes);
            for (int i = 0; i < line.Length; i++)
            {
                string[] data = line[i].Split(' '); //Server ID and prefix
                if (data.Length != 2) continue; //Skips invalid lines
                if (!ulong.TryParse(data[0], out ulong ID)) continue; //Gets ID; Skips non-valid ID numbers
                string prefix = data[1].Trim();

                prefixes.Add(ID, prefix);
            }

            logger.Log(LogSeverity.Info, $"Loaded prefixes from {BotFile.Prefixes}");
        }

        private void LoadGames()
        {
            gameInstances = new List<PacManGame>();

            string[] files = Directory.GetFiles(PacManGame.Folder);
            for (int i = 0; i < files.Length; i++)
            {
                try
                {
                    if (ulong.TryParse(files[i].Replace(PacManGame.Folder, "").Replace(PacManGame.Extension, ""), out ulong channelId))
                    {
                        PacManGame game = new PacManGame(channelId, 1, null, client, this);
                        game.LoadFromFile();
                        gameInstances.Add(game);
                    }
                }
                catch (Exception e)
                {
                    logger.Log(LogSeverity.Error, $"{e}");
                    continue;
                }
            }

            logger.Log(LogSeverity.Info, $"Loaded {gameInstances.Count} games from previous session.");
        }
    }
}
