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
        private readonly DiscordSocketClient _client;
        private readonly CommandService _commands;
        private readonly IConfigurationRoot _config;

        //DiscordSocketClient, CommandService and IConfigurationRoot are injected automatically from the IServiceProvider
        public StartupService(DiscordSocketClient client, CommandService commands, IConfigurationRoot config)
        {
            _client = client;
            _commands = commands;
            _config = config;
        }


        public async Task StartAsync()
        {
            if (!File.Exists(Program.File_Config)) throw new Exception($"Missing {Program.File_Config}: Bot can't run.");
            if (!File.Exists(Program.File_GameMap)) throw new Exception($"Missing {Program.File_GameMap}: Bot can't run.");

            string[] secondaryFiles = new string[] { Program.File_Prefixes, Program.File_Scoreboard, Program.File_About, Program.File_Tips, Program.File_CustomMapHelp };
            for (int i = 0; i < secondaryFiles.Length; i++)
            {
                if (!File.Exists(secondaryFiles[i]))
                {
                    File.Create(secondaryFiles[i]);
                    Console.WriteLine($"Created missing file {secondaryFiles[i]}");
                }
            }

            CommandHandler.prefixes = new Dictionary<ulong, string>(); //Load prefixes from file
            string[] line = File.ReadAllLines(Program.File_Prefixes);
            for (int i = 0; i < line.Length; i++)
            {
                string[] data = line[i].Split(' '); //Server ID and prefix
                if (data.Length != 2) continue; //Skips invalid lines
                if (!ulong.TryParse(data[0], out ulong ID)) continue; //Gets ID; Skips non-valid ID numbers
                string prefix = data[1].Trim();

                CommandHandler.prefixes.Add(ID, prefix);
            }
            Console.WriteLine($"Loaded prefixes from {Program.File_Prefixes}");


            string discordToken = _config["token"]; //Get the discord token from the config file
            if (string.IsNullOrWhiteSpace(discordToken)) throw new Exception($"Please enter the bot's token into the {Program.File_Config} file");

            await _client.LoginAsync(TokenType.Bot, discordToken); //Login to discord
            await _client.StartAsync(); //Connect to the websocket

            await _commands.AddModulesAsync(Assembly.GetEntryAssembly()); //Load commands and modules into the command service
        }
    }
}
