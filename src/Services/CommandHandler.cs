using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Net;
using Discord.WebSocket;

namespace PacManBot.Services
{
    public class CommandHandler
    {
        private readonly DiscordShardedClient client;
        private readonly CommandService commands;
        private readonly StorageService storage;
        private readonly LoggingService logger;
        private readonly IServiceProvider provider;

        public readonly Regex waka = new Regex(@"^(w+a+k+a+\W*)+$", RegexOptions.IgnoreCase);


        public CommandHandler(DiscordShardedClient client, CommandService commands, StorageService storage, LoggingService logger, IServiceProvider provider)
        {
            this.client = client;
            this.commands = commands;
            this.storage = storage;
            this.logger = logger;
            this.provider = provider;

            //Events
            client.MessageReceived += OnMessageReceived;
        }



        private Task OnMessageReceived(SocketMessage m)
        {
            _ = OnMessageReceivedAsync(m); // Discarding allows the async code to run without blocking the gateway task
            return Task.CompletedTask;
        }


        private async Task OnMessageReceivedAsync(SocketMessage m) //I have to do this so that exceptions don't go silent
        {
            try
            {
                await OnMessageReceivedInternal(m);
            }
            catch (Exception e)
            {
                await logger.Log(LogSeverity.Error, $"{e}");
            }
        }


        private async Task OnMessageReceivedInternal(SocketMessage genericMessage)
        {
            if (client.CurrentUser == null) return; // Not ready
            if (!(genericMessage is SocketUserMessage message) || message.Author.IsBot) return;

            var context = new ShardedCommandContext(client, message);

            if (!context.BotCan(ChannelPermission.SendMessages)) return;

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

                    if (reply != null && context.BotCan(ChannelPermission.SendMessages))
                    {
                        await context.Channel.SendMessageAsync(reply, options: Utils.DefaultRequestOptions);
                    }
                }
            }
            else //waka
            {
                if (waka.IsMatch(message.ToString()) && !storage.WakaExclude.Contains($"{context.Guild.Id}") && context.BotCan(ChannelPermission.SendMessages))
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
                                                                      : $"This bot is missing the permission**{Regex.Replace(error.Split(' ').Last(), @"([A-Z])", @" $1")}**!";
            if (error.Contains("User requires")) return guild == null ? "You need to be in a guild to use this command!"
                                                                      : $"You need the permission**{Regex.Replace(error.Split(' ').Last(), @"([A-Z])", @" $1")}** to use this command!";
            if (error.Contains("User not found")) return $"Can't find the specified user!";
            if (error.Contains("Failed to parse")) return $"Invalid command parameters! {help}";
            if (error.Contains("too few parameters")) return $"Missing command parameters! {help}";
            if (error.Contains("too many parameters")) return $"Too many parameters! {help}";
            if (error.Contains("must be used in a guild")) return $"You need to be in a guild to use this command!";

            return null;
        }
    }
}
