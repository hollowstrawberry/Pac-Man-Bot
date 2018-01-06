using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;
using System;

namespace PacManBot.Services
{
    public class CommandHandler
    {
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

        private async Task OnMessageReceived(SocketUserMessage message)
        {
            if (message == null || message.Author.IsBot) return;

            var context = new SocketCommandContext(discord, message);

            int commandPosition = 0; //Where the command will start
            if (message.HasStringPrefix(config["prefix"], ref commandPosition) || message.HasMentionPrefix(discord.CurrentUser, ref commandPosition)) //If the message's prefix matches
            {
                var result = await commands.ExecuteAsync(context, commandPosition, provider); //Try to execute the command
                if (!result.IsSuccess)
                {
                    if (!result.ErrorReason.ToString().Contains("Unknown command")) Console.WriteLine($"{DateTime.UtcNow.ToString("hh: mm:ss")} Command {message} by {message.Author.Username}#{message.Author.Discriminator} couldn't be executed. Reason: {result.ErrorReason.ToString()}");

                    if      (result.ErrorReason.Contains("Bot requires guild permission")) await context.Channel.SendMessageAsync($"This bot requires the permission {result.ErrorReason.Split(' ')[result.ErrorReason.Split(' ').Length - 1]}!");
                    else if (result.ErrorReason.Contains("User requires guild permission")) await context.Channel.SendMessageAsync($"You need to be a moderator to use this command!");
                }
            }
            else
            {
                if (message.ToString().ToLower() == "waka") await context.Channel.SendMessageAsync("waka");
            }
        }
    }
}
