using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using PacManBot.Constants;

namespace PacManBot.Services
{
    public class StartupService
    {
        private readonly DiscordSocketClient _client;
        private readonly CommandService _commands;
        private readonly LoggingService _logger;
        private readonly IConfigurationRoot _config;

        //DiscordSocketClient, CommandService and IConfigurationRoot are injected automatically from the IServiceProvider
        public StartupService(DiscordSocketClient client, CommandService commands, LoggingService logger, IConfigurationRoot config)
        {
            _client = client;
            _commands = commands;
            _logger = logger;
            _config = config;
        }


        public async Task StartAsync()
        {
            if (!File.Exists(BotFile.Config)) throw new Exception($"Missing {BotFile.Config}: Bot can't run.");
            if (!File.Exists(BotFile.GameMap)) throw new Exception($"Missing {BotFile.GameMap}: Bot can't run.");

            string[] secondaryFiles = new string[] { BotFile.Prefixes, BotFile.Scoreboard, BotFile.About, BotFile.Tips, BotFile.CustomMapHelp };
            for (int i = 0; i < secondaryFiles.Length; i++)
            {
                if (!File.Exists(secondaryFiles[i]))
                {
                    File.Create(secondaryFiles[i]);
                    await _logger.Log(LogSeverity.Info, $"Created missing file {secondaryFiles[i]}");
                }
            }

            CommandHandler.prefixes = new Dictionary<ulong, string>(); //Load prefixes from file
            string[] line = File.ReadAllLines(BotFile.Prefixes);
            for (int i = 0; i < line.Length; i++)
            {
                string[] data = line[i].Split(' '); //Server ID and prefix
                if (data.Length != 2) continue; //Skips invalid lines
                if (!ulong.TryParse(data[0], out ulong ID)) continue; //Gets ID; Skips non-valid ID numbers
                string prefix = data[1].Trim();

                CommandHandler.prefixes.Add(ID, prefix);
            }
            await _logger.Log(LogSeverity.Info, $"Loaded prefixes from {BotFile.Prefixes}");


            string discordToken = _config["token"]; //Get the discord token from the config file
            if (string.IsNullOrWhiteSpace(discordToken)) throw new Exception($"Please enter the bot's token into the {BotFile.Config} file");

            await _client.LoginAsync(TokenType.Bot, discordToken); //Login to discord
            await _client.StartAsync(); //Connect to the websocket

            await _commands.AddModulesAsync(Assembly.GetEntryAssembly()); //Load commands and modules into the command service
        }
    }
}
