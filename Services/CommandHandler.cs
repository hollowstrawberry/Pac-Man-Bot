using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace PacManBot.Services
{
    public class CommandHandler
    {
        private readonly DiscordSocketClient client;
        private readonly CommandService commands;
        private readonly StorageService storage;
        private readonly LoggingService logger;
        private readonly IServiceProvider provider;
        public readonly Regex waka = new Regex(@"^(w+a+k+a+\W*)+$", RegexOptions.IgnoreCase);


        public CommandHandler(DiscordSocketClient client, CommandService commands, StorageService storage, LoggingService logger, IServiceProvider provider)
        {
            this.client = client;
            this.commands = commands;
            this.storage = storage;
            this.logger = logger;
            this.provider = provider;

            this.client.MessageReceived += OnMessageReceived;
        }


        private Task OnMessageReceived(SocketMessage genericMessage)
        {
            Task.Run(async () => //Wrapping in a Task.Run prevents the gateway from getting blocked in case something goes wrong
            {
                var message = genericMessage as SocketUserMessage;
                if (message == null || message.Author.IsBot) return;

                var context = new SocketCommandContext(client, message);
                string prefix = storage.GetPrefix(context.Guild);
                int commandPosition = 0;

                if (message.HasMentionPrefix(client.CurrentUser, ref commandPosition) || message.HasStringPrefix($"{prefix} ", ref commandPosition) || message.HasStringPrefix(prefix, ref commandPosition) || context.Channel is IDMChannel)
                {
                    var result = await commands.ExecuteAsync(context, commandPosition, provider); //Try to execute the command
                    if (!result.IsSuccess)
                    {
                        string error = result.ErrorReason;
                        if (!error.Contains("Unknown command")) await logger.Log(LogSeverity.Error, $"Command {message} by {message.Author.FullName()} in channel {context.FullChannelName()} couldn't be executed. {error}");

                        string help = $"Please use **{storage.GetPrefixOrEmpty(context.Guild)}help [command name]** or try again.";
                        if      (error.Contains("Bot requires")) await context.Channel.SendMessageAsync(context.Guild == null ? "You need to be in a guild to use this command!" : $"This bot requires the permission {result.ErrorReason.Split(' ')[result.ErrorReason.Split(' ').Length - 1]}!");
                        else if (error.Contains("User requires")) await context.Channel.SendMessageAsync(context.Guild == null ? "You need to be in a guild to use this command!" : $"You need the permission {result.ErrorReason.Split(' ')[result.ErrorReason.Split(' ').Length - 1]} to use this command!");
                        else if (error.Contains("User not found")) await context.Channel.SendMessageAsync($"Can't find the specified user!");
                        else if (error.Contains("Failed to parse")) await context.Channel.SendMessageAsync($"Invalid command parameters! {help}");
                        else if (error.Contains("too few parameters")) await context.Channel.SendMessageAsync($"Missing command parameters! {help}");
                        else if (error.Contains("too many parameters")) await context.Channel.SendMessageAsync($"Too many parameters! {help}");
                        else if (error.Contains("must be used in a guild")) await context.Channel.SendMessageAsync($"You need to be in a guild to use this command!");
                    }
                }

                else //waka
                {
                    if (waka.IsMatch(message.ToString()) && !storage.wakaExclude.Contains($"{context.Guild.Id}"))
                    {
                        await context.Channel.SendMessageAsync("waka");
                        await logger.Log(LogSeverity.Verbose, $"Waka at {context.FullChannelName()}");
                    }
                }
            });

            return Task.CompletedTask;
        }
    }
}
