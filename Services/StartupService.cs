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
        private readonly DiscordSocketClient client;
        private readonly CommandService commands;
        private readonly LoggingService logger;
        private readonly IConfigurationRoot config;

        //DiscordSocketClient, CommandService and IConfigurationRoot are injected automatically from the IServiceProvider
        public StartupService(DiscordSocketClient client, CommandService commands, LoggingService logger, IConfigurationRoot config)
        {
            this.client = client;
            this.commands = commands;
            this.logger = logger;
            this.config = config;
        }


        public async Task StartAsync()
        {
            if (!File.Exists(BotFile.Config)) throw new Exception($"Missing {BotFile.Config}: Bot can't run.");
            if (!File.Exists(BotFile.GameMap)) throw new Exception($"Missing {BotFile.GameMap}: Bot can't run.");

            string[] secondaryFiles = new string[] { BotFile.Prefixes, BotFile.Scoreboard, BotFile.About, BotFile.GameHelp, BotFile.CustomMapHelp, BotFile.InviteLink, BotFile.WakaExclude };
            for (int i = 0; i < secondaryFiles.Length; i++)
            {
                if (!File.Exists(secondaryFiles[i]))
                {
                    File.Create(secondaryFiles[i]).Dispose();
                    await logger.Log(LogSeverity.Warning, $"Created missing file \"{secondaryFiles[i]}\"");
                }
            }


            string discordToken = config["token"]; //Get the discord token from the config file
            if (string.IsNullOrWhiteSpace(discordToken)) throw new Exception($"Please enter the bot's token into the {BotFile.Config} file");

            await client.LoginAsync(TokenType.Bot, discordToken); //Login to discord
            await client.StartAsync(); //Connect to the websocket

            await commands.AddModulesAsync(Assembly.GetEntryAssembly()); //Load commands and modules into the command service
        }
    }
}
