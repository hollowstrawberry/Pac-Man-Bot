using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using PacManBot.Services;
using PacManBot.Constants;

namespace PacManBot.Modules
{
    [Name("<:staff:412019879772815361>Mod")]
    public class ModModule : ModuleBase<SocketCommandContext>
    {
        private readonly LoggingService logger;
        private readonly StorageService storage;


        string ErrorMessage => $"Please try again or, if the problem persists, contact the bot author using **{storage.GetPrefixOrEmpty(Context.Guild)}feedback**.";


        public ModModule(LoggingService logger, StorageService storage)
        {
            this.logger = logger;
            this.storage = storage;
        }



        [Command("say"), Remarks("Make the bot say anything")]
        [Summary("Repeats back the message provided. Only users with the Manage Messages permission can use this command.")]
        [RequireUserPermission(ChannelPermission.ManageMessages)]
        public async Task Say([Remainder]string message) => await ReplyAsync(message.SanitizeMentions());


        [Command("clear"), Alias("c"), Remarks("Clear messages from this bot")]
        [Summary("Clears all messages sent by *this bot only*, checking up to the amount of messages provided, or 10 messages by default. "
               + "Only users with the Manage Messages permission can use this command.")]
        [RequireUserPermission(ChannelPermission.ManageMessages), RequireBotPermission(ChannelPermission.ReadMessageHistory)]
        public async Task ClearGameMessages(int amount = 10)
        {
            var messages = await Context.Channel.GetMessagesAsync(amount).FlattenAsync();
            foreach (IMessage message in messages)
            {
                try
                {
                    if (message.Author.Id == Context.Client.CurrentUser.Id) await message.DeleteAsync(); //Remove all messages from this bot
                }
                catch (Discord.Net.HttpException e)
                {
                    await logger.Log(LogSeverity.Warning, $"Couldn't delete message {message.Id} in {Context.Channel.FullName()}: {e.Message}");
                }
            }
        }


        [Command("setprefix"), Remarks("Set a custom prefix for this server (Admin)")]
        [Summary("Change the custom prefix for this server. Only server Administrators can use this command.\n"
               + "Prefixes can't contain these characters: \\* \\_ \\~ \\` \\\\")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task SetServerPrefix(string prefix)
        {
            if (prefix.ContainsAny("*", "_", "~", "`", "\\"))
            {
                await ReplyAsync($"{CustomEmoji.Cross} The prefix can't contain markdown special characters: *_~\\`\\\\");
                return;
            }

            try
            {
                storage.SetPrefix(Context.Guild.Id, prefix);
                await ReplyAsync($"{CustomEmoji.Check} Prefix for this server has been successfully set to '{prefix}'.");
                await logger.Log(LogSeverity.Info, $"Prefix for server {Context.Guild.Id} set to {prefix}");
            }
            catch (Exception e)
            {
                await logger.Log(LogSeverity.Error, $"{e}");
                await ReplyAsync($"{CustomEmoji.Cross} There was a problem setting the prefix. {ErrorMessage}");
            }
        }


        [Command("togglewaka"), Remarks("Toggle \"waka\" autoresponse from the bot")]
        [Summary("The bot normally responds every time a message contains purely multiples of \"waka\", unless it's turned off server-wide using this command. Requires the user to be a Moderator.")]
        [RequireUserPermission(GuildPermission.ManageMessages)]
        public async Task ToggleWakaResponse()
        {
            try
            {
                bool nowaka = storage.ToggleWaka(Context.Guild.Id);
                await ReplyAsync($"{CustomEmoji.Check} \"Waka\" responses turned **{(nowaka ? "on" : "off")}** in this server.");
            }
            catch (Exception e)
            {
                await logger.Log(LogSeverity.Error, $"{e}");
                await ReplyAsync($"{CustomEmoji.Cross} Oops, something went wrong. {ErrorMessage}");
            }
        }
    }
}
