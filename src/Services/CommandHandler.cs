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


        private Task OnMessageReceived(SocketMessage m)
        {
            Task.Run(async () => await OnMessageReceivedAsync(m));  // Prevents the gateway task from getting blocked
            return Task.CompletedTask;
        }


        private async Task OnMessageReceivedAsync(SocketMessage genericMessage)
        {
            var message = genericMessage as SocketUserMessage;
            if (message == null || message.Author.IsBot) return;

            var context = new SocketCommandContext(client, message);
            if (context.Channel is SocketGuildChannel && !context.BotHas(ChannelPermission.SendMessages)) return;

            string prefix = storage.GetPrefix(context.Guild);
            int commandPosition = 0;

            if (message.HasMentionPrefix(client.CurrentUser, ref commandPosition) || message.HasStringPrefix($"{prefix} ", ref commandPosition) || message.HasStringPrefix(prefix, ref commandPosition) || context.Channel is IDMChannel)
            {
                var result = await commands.ExecuteAsync(context, commandPosition, provider); //Try to execute the command
                if (!result.IsSuccess)
                {
                    string error = result.ErrorReason;
                    if (!error.Contains("Unknown command")) await logger.Log(LogSeverity.Verbose, $"Command {message} by {message.Author.FullName()} in channel {context.Channel.FullName()} couldn't be executed. {error}");

                    string reply = GetCommandErrorReply(error, context.Guild);

                    if (reply != null && (context.Channel is IDMChannel || context.BotHas(ChannelPermission.SendMessages)))
                    {
                        await context.Channel.SendMessageAsync(reply);
                    }
                }
            }

            else //waka
            {
                if (waka.IsMatch(message.ToString()) && !storage.WakaExclude.Contains($"{context.Guild.Id}") && context.BotHas(ChannelPermission.SendMessages))
                {
                    await context.Channel.SendMessageAsync("waka");
                    await logger.Log(LogSeverity.Verbose, $"Waka at {context.Channel.FullName()}");
                }
            }
        }


        private string GetCommandErrorReply(string error, SocketGuild guild)
        {
            string help = $"Please use **{storage.GetPrefixOrEmpty(guild)}help [command name]** or try again.";

            if (error.Contains("Bot requires"))  return guild == null ? "You need to be in a guild to use this command!"
                                                                      : $"This bot requires the permission {error.Split(' ').Last()}!";
            if (error.Contains("User requires")) return guild == null ? "You need to be in a guild to use this command!"
                                                                      : $"You need the permission {error.Split(' ').Last()} to use this command!";
            if (error.Contains("User not found")) return $"Can't find the specified user!";
            if (error.Contains("Failed to parse")) return $"Invalid command parameters! {help}";
            if (error.Contains("too few parameters")) return $"Missing command parameters! {help}";
            if (error.Contains("too many parameters")) return $"Too many parameters! {help}";
            if (error.Contains("must be used in a guild")) return $"You need to be in a guild to use this command!";

            return null;
        }
    }
}
