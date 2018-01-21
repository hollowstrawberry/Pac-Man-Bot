using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;

namespace PacManBot.Services
{
    public class CommandHandler
    {
        public static Dictionary<ulong, string> prefixes;

        private readonly DiscordSocketClient discord;
        private readonly CommandService commands;
        private readonly IConfigurationRoot config;
        private readonly IServiceProvider provider;

        //DiscordSocketClient, CommandService, IConfigurationRoot, and IServiceProvider are injected automatically from the IServiceProvider
        public CommandHandler(DiscordSocketClient discord, CommandService commands, IConfigurationRoot config, IServiceProvider provider)
        {
            this.discord = discord;
            this.commands = commands;
            this.config = config;
            this.provider = provider;

            this.discord.MessageReceived += (m) => OnMessageReceived(m as SocketUserMessage);
        }

        private Task OnMessageReceived(SocketUserMessage message)
        {
            if (message == null || message.Author.IsBot || prefixes == null) return Task.CompletedTask;

            var context = new SocketCommandContext(discord, message);

            string prefix = config["prefix"]; //Gets the prefix for the current server or uses the default one if not found
            if (context.Guild != null && !prefixes.TryGetValue(context.Guild.Id, out prefix)) prefix = config["prefix"]; 

            int commandPosition = 0; //Where the command will start
            if (message.HasMentionPrefix(discord.CurrentUser, ref commandPosition) || message.HasStringPrefix($"{prefix} ", ref commandPosition) || message.HasStringPrefix(prefix, ref commandPosition) || context.Channel is IDMChannel)
            {
                Task.Run(async () => //Wrapping in a Task.Run prevents the gateway from getting blocked in case something goes wrong
                {
                    var result = await commands.ExecuteAsync(context, commandPosition, provider); //Try to execute the command
                    if (!result.IsSuccess)
                    {
                        if (!result.ErrorReason.ToString().Contains("Unknown command")) Console.WriteLine($"{DateTime.UtcNow.ToString("hh: mm:ss")} Command {message} by {message.Author.Username}#{message.Author.Discriminator} couldn't be executed. Reason: {result.ErrorReason.ToString()}");

                        if (result.ErrorReason.Contains("Bot requires")) await context.Channel.SendMessageAsync($"This bot requires the permission {result.ErrorReason.Split(' ')[result.ErrorReason.Split(' ').Length - 1]}!");
                        else if (result.ErrorReason.Contains("User requires")) await context.Channel.SendMessageAsync($"You need the permission {result.ErrorReason.Split(' ')[result.ErrorReason.Split(' ').Length - 1]} to use this command!");
                        else if (result.ErrorReason.Contains("User not found")) await context.Channel.SendMessageAsync($"Can't find the specified user!");
                        else if (result.ErrorReason.Contains("Failed to parse") || result.ErrorReason.Contains("parameters")) await context.Channel.SendMessageAsync($"Incorrect command parameters!");
                    }
                });
            }

            else //waka
            {
                if (message.ToString().ToLower().StartsWith("waka")) context.Channel.SendMessageAsync("waka");
            }

            return Task.CompletedTask;
        }
    }
}
