using System.IO;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Discord.WebSocket;
using PacManBot.Constants;
using PacManBot.Modules.PacMan;

namespace PacManBot.Services
{
    public class StorageService
    {
        private readonly LoggingService _logger;

        public readonly string defaultPrefix;
        public Dictionary<ulong, string> prefixes;
        public List<Game> gameInstances;

        public StorageService(LoggingService logger, IConfigurationRoot config)
        {
            _logger = logger;

            defaultPrefix = config["prefix"];
            prefixes = new Dictionary<ulong, string>();
            gameInstances = new List<Game>();

            //Load prefixes from file
            string[] line = File.ReadAllLines(BotFile.Prefixes);
            for (int i = 0; i < line.Length; i++)
            {
                string[] data = line[i].Split(' '); //Server ID and prefix
                if (data.Length != 2) continue; //Skips invalid lines
                if (!ulong.TryParse(data[0], out ulong ID)) continue; //Gets ID; Skips non-valid ID numbers
                string prefix = data[1].Trim();

                prefixes.Add(ID, prefix);
            }

            _logger.Log(Discord.LogSeverity.Info, $"Loaded prefixes from {BotFile.Prefixes}");
        }


        public string GetPrefix(SocketGuild guild = null)
        {
            return (guild == null) ? defaultPrefix : GetPrefix(guild.Id);
        }
        public string GetPrefix(ulong serverId)
        {
            return (prefixes.ContainsKey(serverId)) ? prefixes[serverId] : defaultPrefix;
        }

        public string GetPrefixOrEmpty(SocketGuild guild)
        {
            return (guild == null) ? "" : GetPrefix(guild.Id);
        }
    }
}
