using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;

namespace PacManBot.Services
{
    public class StartupService
    {
        private readonly DiscordSocketClient discord;
        private readonly CommandService commands;
        private readonly IConfigurationRoot config;

        //DiscordSocketClient, CommandService and IConfigurationRoot are injected automatically from the IServiceProvider
        public StartupService(DiscordSocketClient discord, CommandService commands, IConfigurationRoot config)
        {
            this.discord = discord;
            this.commands = commands;
            this.config = config;
        }


        public async Task StartAsync()
        {
            CommandHandler.prefixes = new Dictionary<ulong, string>(); //Load prefixes from file
            string[] line = File.ReadAllLines(Program.File_Prefixes);
            for (int i = 0; i < line.Length; i++)
            {
                string[] data = line[i].Split(' '); //Server ID and prefix
                if (data.Length != 2) continue;

                if (!ulong.TryParse(data[0], out ulong ID)) continue; //Skips non-valid ID numbers
                string prefix = data[1].Trim();

                CommandHandler.prefixes.Add(ID, prefix);
            }
            Console.WriteLine("Loaded prefixes");


            string discordToken = config["token"]; //Get the discord token from the config file
            if (string.IsNullOrWhiteSpace(discordToken)) throw new Exception("Please enter the bot's token into the bot_config.json file");

            await discord.LoginAsync(TokenType.Bot, discordToken); //Login to discord
            await discord.StartAsync(); //Connect to the websocket

            await commands.AddModulesAsync(Assembly.GetEntryAssembly()); //Load commands and modules into the command service
        }
    }
}
