using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using PacManBot.Constants;

namespace PacManBot.Services
{
    public class CommandHandler
    {
        public static Dictionary<ulong, string> prefixes;

        private readonly DiscordSocketClient _client;
        private readonly CommandService _commands;
        private readonly LoggingService _logger;
        private readonly IServiceProvider _provider;
        private readonly IConfigurationRoot _config;

        //DiscordSocketClient, CommandService, IConfigurationRoot, and IServiceProvider are injected automatically from the IServiceProvider
        public CommandHandler(DiscordSocketClient client, CommandService commands, LoggingService logger, IServiceProvider provider, IConfigurationRoot config)
        {
            _client = client;
            _commands = commands;
            _logger = logger;
            _provider = provider;
            _config = config;

            _client.MessageReceived += (m) => OnMessageReceived(m as SocketUserMessage);
        }

        private Task OnMessageReceived(SocketUserMessage message)
        {
            if (message == null || message.Author.IsBot || prefixes == null) return Task.CompletedTask;

            var context = new SocketCommandContext(_client, message);

            string prefix = _config["prefix"]; //Gets the prefix for the current server or uses the default one if not found
            if (context.Guild != null && !prefixes.TryGetValue(context.Guild.Id, out prefix)) prefix = _config["prefix"]; 

            int commandPosition = 0; //Where the command will start
            if (message.HasMentionPrefix(_client.CurrentUser, ref commandPosition) || message.HasStringPrefix($"{prefix} ", ref commandPosition) || message.HasStringPrefix(prefix, ref commandPosition) || context.Channel is IDMChannel)
            {
                Task.Run(async () => //Wrapping in a Task.Run prevents the gateway from getting blocked in case something goes wrong
                {
                    var result = await _commands.ExecuteAsync(context, commandPosition, _provider); //Try to execute the command
                    if (!result.IsSuccess)
                    {
                        if (!result.ErrorReason.ToString().Contains("Unknown command")) await _logger.Log(LogSeverity.Error, $"Command {message} by {message.Author.Username}#{message.Author.Discriminator} couldn't be executed. Reason: {result.ErrorReason.ToString()}");

                        if (result.ErrorReason.Contains("Bot requires")) await context.Channel.SendMessageAsync(context.Guild == null ? "You need to be in a guild to use this command!" : $"This bot requires the permission {result.ErrorReason.Split(' ')[result.ErrorReason.Split(' ').Length - 1]}!");
                        else if (result.ErrorReason.Contains("User requires")) await context.Channel.SendMessageAsync(context.Guild == null ? "You need to be in a guild to use this command!" : $"You need the permission {result.ErrorReason.Split(' ')[result.ErrorReason.Split(' ').Length - 1]} to use this command!");
                        else if (result.ErrorReason.Contains("User not found")) await context.Channel.SendMessageAsync($"Can't find the specified user!");
                        else if (result.ErrorReason.Contains("Failed to parse") || result.ErrorReason.Contains("parameters")) await context.Channel.SendMessageAsync($"Incorrect command parameters!");
                        else if (result.ErrorReason.Contains("must be used in a guild")) await context.Channel.SendMessageAsync($"You need to be in a guild to use this command!");
                    }
                });
            }

            else //waka
            {
                if (message.ToString().ToLower().StartsWith("waka"))
                {
                    context.Channel.SendMessageAsync("waka");
                    _logger.Log(LogSeverity.Verbose, $"Waka at {(message.Author as SocketGuildUser != null ? $"{(message.Author as SocketGuildUser).Guild.Name}/" : "")}message.Channel");
                }
            }

            return Task.CompletedTask;
        }

        public static string ServerPrefix(SocketGuild guild) => guild == null ? null : ServerPrefix(guild.Id);
        public static string ServerPrefix(ulong serverId)
        {
            string prefix = null;
            if (prefixes.ContainsKey(serverId)) prefix = prefixes[serverId];
            return prefix;
        }
    }
}
