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


        public StartupService(DiscordSocketClient client, CommandService commands, LoggingService logger, IConfigurationRoot config)
        {
            this.client = client;
            this.commands = commands;
            this.logger = logger;
            this.config = config;
        }


        public async Task StartAsync()
        {
            string[] essentialFile = new string[] { BotFile.Config, BotFile.Contents };
            for (int i = 0; i < essentialFile.Length; i++)
            {
                if (!File.Exists(essentialFile[i])) throw new Exception($"Missing {essentialFile[i]}: Bot can't run.");
            }

            string[] secondaryFile = new string[] { BotFile.Prefixes, BotFile.Scoreboard, BotFile.WakaExclude };
            for (int i = 0; i < secondaryFile.Length; i++)
            {
                if (!File.Exists(secondaryFile[i]))
                {
                    File.Create(secondaryFile[i]).Dispose();
                    await logger.Log(LogSeverity.Warning, $"Created missing file \"{secondaryFile[i]}\"");
                }
            }


            string discordToken = config["token"]; //Get the discord token from the config file
            if (string.IsNullOrWhiteSpace(discordToken)) throw new Exception($"Missing bot token in {BotFile.Config}");

            await client.LoginAsync(TokenType.Bot, discordToken); //Login to discord
            await client.StartAsync(); //Connect to the websocket

            await commands.AddModulesAsync(Assembly.GetEntryAssembly()); //Load commands and modules into the command service
        }
    }
}
