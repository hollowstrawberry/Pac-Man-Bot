using System;
using System.Reflection;
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
            string discordToken = config["token"]; //Get the discord token from the config file
            if (string.IsNullOrWhiteSpace(discordToken))
            {
                throw new Exception("Please enter the bot's token into the config.json file");
            }

            await discord.LoginAsync(TokenType.Bot, discordToken); //Login to discord
            await discord.StartAsync(); //Connect to the websocket
            await discord.SetGameAsync($"{config["prefix"]}help"); //"Playing" message

            await commands.AddModulesAsync(Assembly.GetEntryAssembly()); //Load commands and modules into the command service
        }
    }
}
