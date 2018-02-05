using System;
using System.IO;
using System.Reflection;
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

            string[] secondaryFiles = new string[] { BotFile.Prefixes, BotFile.Scoreboard, BotFile.About, BotFile.GameHelp, BotFile.CustomMapHelp, BotFile.InviteLink };
            for (int i = 0; i < secondaryFiles.Length; i++)
            {
                if (!File.Exists(secondaryFiles[i]))
                {
                    File.Create(secondaryFiles[i]).Dispose();
                    await _logger.Log(LogSeverity.Warning, $"Created missing file {secondaryFiles[i]}");
                }
            }


            string discordToken = _config["token"]; //Get the discord token from the config file
            if (string.IsNullOrWhiteSpace(discordToken)) throw new Exception($"Please enter the bot's token into the {BotFile.Config} file");

            await _client.LoginAsync(TokenType.Bot, discordToken); //Login to discord
            await _client.StartAsync(); //Connect to the websocket

            await _commands.AddModulesAsync(Assembly.GetEntryAssembly()); //Load commands and modules into the command service
        }
    }
}
