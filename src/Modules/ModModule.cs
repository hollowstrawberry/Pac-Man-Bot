using System;
using System.Threading.Tasks;
using Discord;
using Discord.Net;
using Discord.Commands;
using PacManBot.Utils;
using PacManBot.Services;
using PacManBot.Extensions;

namespace PacManBot.Modules
{
    [Name(CustomEmoji.Staff + "Mod"), Remarks("5")]
    [BetterRequireUserPermission(GuildPermission.ManageMessages)]
    public class ModModule : ModuleBase<SocketCommandContext>
    {
        private readonly LoggingService logger;
        private readonly StorageService storage;


        string ErrorMessage => $"Please try again or, if the problem persists, contact the bot author using `{storage.GetPrefixOrEmpty(Context.Guild)}feedback`.";


        public ModModule(LoggingService logger, StorageService storage)
        {
            this.logger = logger;
            this.storage = storage;
        }



        [Command("say"), Remarks("Make the bot say anything")]
        [Summary("Repeats back the message provided. Only users with the Manage Messages permission can use this command.")]
        public async Task Say([Remainder]string message) => await ReplyAsync(message.SanitizeMentions(), options: Bot.DefaultOptions);


        [Command("clear"), Alias("cl"), Remarks("Clear this bot's messages and commands")]
        [Summary("Clears all commands and messages for *this bot only*, from the last [amount] messages, or the last 10 messages by default.\n"
               + "Only users with the Manage Messages permission can use this command.")]
        [BetterRequireBotPermission(ChannelPermission.ReadMessageHistory)]
        public async Task ClearGameMessages(int amount = 10)
        {
            foreach (IMessage message in await Context.Channel.GetMessagesAsync(amount).FlattenAsync())
            {
                try
                {
                    if (message.Author.Id == Context.Client.CurrentUser.Id || message.Content.StartsWith(storage.GetPrefix(Context.Guild)) && Context.BotCan(ChannelPermission.ManageMessages))
                    {
                        await message.DeleteAsync(Bot.DefaultOptions);
                    }
                }
                catch (Exception e) when (e is HttpException || e is TimeoutException)
                {
                    await logger.Log(LogSeverity.Warning, $"Couldn't delete message {message.Id} in {Context.Channel.FullName()}: {e.Message}");
                }
            }
        }


        [Command("setprefix"), Remarks("Set a custom prefix for this server (Admin)")]
        [Summary("Change the custom prefix for this server. Only server Administrators can use this command.\n"
               + "Prefixes can't contain these characters: \\* \\_ \\~ \\` \\\\")]
        [BetterRequireUserPermission(GuildPermission.Administrator)]
        public async Task SetServerPrefix(string prefix)
        {
            if (prefix.ContainsAny("*", "_", "~", "`", "\\"))
            {
                await ReplyAsync($"{CustomEmoji.Cross} The prefix can't contain markdown special characters: *_~\\`\\\\", options: Bot.DefaultOptions);
                return;
            }

            try
            {
                storage.SetPrefix(Context.Guild.Id, prefix);
                await ReplyAsync($"{CustomEmoji.ECheck} Prefix for this server has been successfully set to '{prefix}'.", options: Bot.DefaultOptions);
                await logger.Log(LogSeverity.Info, $"Prefix for server {Context.Guild.Id} set to {prefix}");
            }
            catch (Exception e)
            {
                await logger.Log(LogSeverity.Error, $"{e}");
                await ReplyAsync($"{CustomEmoji.Cross} There was a problem setting the prefix. {ErrorMessage}", options: Bot.DefaultOptions);
            }
        }


        [Command("togglewaka"), Remarks("Toggle \"waka\" autoresponse from the bot")]
        [Summary("The bot normally responds every time a message contains purely multiples of \"waka\", unless it's turned off server-wide using this command. Requires the user to be a Moderator.")]
        public async Task ToggleWakaResponse()
        {
            try
            {
                bool nowaka = storage.ToggleWaka(Context.Guild.Id);
                await ReplyAsync($"{CustomEmoji.Check} \"Waka\" responses turned **{(nowaka ? "ON" : "OFF")}** in this server.", options: Bot.DefaultOptions);
            }
            catch (Exception e)
            {
                await logger.Log(LogSeverity.Error, $"{e}");
                await ReplyAsync($"{CustomEmoji.Cross} Oops, something went wrong. {ErrorMessage}", options: Bot.DefaultOptions);
            }
        }
    }
}
