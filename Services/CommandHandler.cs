using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace PacManBot.Services
{
    public class CommandHandler
    {
        private readonly DiscordSocketClient _client;
        private readonly CommandService _commands;
        private readonly StorageService _storage;
        private readonly LoggingService _logger;
        private readonly IServiceProvider _provider;

        public CommandHandler(DiscordSocketClient client, CommandService commands, StorageService storage, LoggingService logger, IServiceProvider provider)
        {
            _client = client;
            _commands = commands;
            _storage = storage;
            _logger = logger;
            _provider = provider;

            _client.MessageReceived += (m) => OnMessageReceived(m as SocketUserMessage);
        }

        private Task OnMessageReceived(SocketUserMessage message)
        {
            if (message == null || message.Author.IsBot || _storage.prefixes == null) return Task.CompletedTask;

            var context = new SocketCommandContext(_client, message);
            string prefix = _storage.GetPrefix(context.Guild);

            int commandPosition = 0; //Where the command will start
            if (message.HasMentionPrefix(_client.CurrentUser, ref commandPosition) || message.HasStringPrefix($"{prefix} ", ref commandPosition) || message.HasStringPrefix(prefix, ref commandPosition) || context.Channel is IDMChannel)
            {
                Task.Run(async () => //Wrapping in a Task.Run prevents the gateway from getting blocked in case something goes wrong
                {
                    var result = await _commands.ExecuteAsync(context, commandPosition, _provider); //Try to execute the command
                    if (!result.IsSuccess)
                    {
                        string error = result.ErrorReason;
                        if (!error.Contains("Unknown command")) await _logger.Log(LogSeverity.Error, $"Command {message} by {message.Author.FullName()} in channel {context.FullChannelName()} couldn't be executed. {error}");

                        string help = $"Please use **{_storage.GetPrefix(context.Guild).If(context.Guild != null)}help** or try again.";
                        if      (error.Contains("Bot requires")) await context.Channel.SendMessageAsync(context.Guild == null ? "You need to be in a guild to use this command!" : $"This bot requires the permission {result.ErrorReason.Split(' ')[result.ErrorReason.Split(' ').Length - 1]}!");
                        else if (error.Contains("User requires")) await context.Channel.SendMessageAsync(context.Guild == null ? "You need to be in a guild to use this command!" : $"You need the permission {result.ErrorReason.Split(' ')[result.ErrorReason.Split(' ').Length - 1]} to use this command!");
                        else if (error.Contains("User not found")) await context.Channel.SendMessageAsync($"Can't find the specified user!");
                        else if (error.Contains("Failed to parse")) await context.Channel.SendMessageAsync($"Invalid command parameters! {help}");
                        else if (error.Contains("too few parameters")) await context.Channel.SendMessageAsync($"Missing command parameters! {help}");
                        else if (error.Contains("too many parameters")) await context.Channel.SendMessageAsync($"Too many parameters! {help}");
                        else if (error.Contains("must be used in a guild")) await context.Channel.SendMessageAsync($"You need to be in a guild to use this command!");
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
    }
}
