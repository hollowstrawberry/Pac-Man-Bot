using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using PacManBot.Constants;
using PacManBot.Extensions;

namespace PacManBot.Commands.Modules
{
    [Name(ModuleNames.Mod), Remarks("5")]
    [PmRequireUserPermission(GuildPermission.ManageMessages)]
    public class ModModule : BaseModule
    {
        string ContactMessage => $"Please try again or, if the problem persists, contact the bot owner using `{Context.Prefix}feedback`.";


        [Command("say"), Remarks("Make the bot say anything")]
        [Summary("Repeats back the message provided. Only users with the Manage Messages permission can use this command.")]
        public async Task Say([Remainder]string message)
            => await ReplyAsync(message.SanitizeMentions());


        [Command("clear"), Alias("clean"), Remarks("Clear this bot's messages and commands")]
        [Summary("Clears all commands and messages for *this bot only*, from the last [amount] messages up to 30, " +
                 "or the last 10 messages by default.\nOnly users with the Manage Messages permission can use this command.")]
        [PmRequireBotPermission(ChannelPermission.ReadMessageHistory)]
        public async Task ClearCommandMessages(int amount = 10)
        {
            var messages = (await Context.Channel.GetMessagesAsync(Math.Min(amount, 30)).FlattenAsync()).OfType<IUserMessage>();

            var toDelete = messages.Where(x => x.Author.Id == Context.Client.CurrentUser.Id);

            if (Context.BotCan(ChannelPermission.ManageMessages))
            {
                toDelete = toDelete.Concat(
                    messages.Where(x => x.Content.StartsWith(Context.Prefix)
                                   || Context.Client.MentionPrefix.IsMatch(x.Content)));
            }

            foreach (var message in toDelete)
            {
                try
                {
                    await message.DeleteAsync(DefaultOptions);
                }
                catch (Exception e)
                {
                    Log.Warning($"Couldn't delete message {message.Id} in {Context.Channel.FullName()}: {e.Message}");
                }
            }
        }


        [Command("setprefix"), Remarks("Set a custom prefix for this server")]
        [Summary("Change the custom prefix for this server. Requires the user to have the Manage Guild permission.\n" +
                 "Prefixes can't contain these characters: \\* \\_ \\~ \\` \\\\")]
        [PmRequireUserPermission(GuildPermission.ManageGuild)]
        public async Task SetServerPrefix(string prefix)
        {
            string error = null;
            if (string.IsNullOrWhiteSpace(prefix))
                error = $"The guild prefix can't be empty. If you don't want a prefix in a channel, check out `{Context.Prefix}toggleprefix`";
            else if (prefix.ContainsAny('*', '_', '~', '`', '\''))
                error = "The prefix can't contain markdown special characters: *_~\\`\\\\";
            else if (prefix.Contains("||"))
                error = "The prefix can't contain \"||\"";
            else if (prefix.Length > 32)
                error = "Prefix can't be bigger than 32 characters";

            if (error != null)
            {
                await ReplyAsync($"{CustomEmoji.Cross} {error}");
                return;
            }


            try
            {
                Storage.SetGuildPrefix(Context.Guild.Id, prefix);
                await ReplyAsync($"{CustomEmoji.Check} Prefix for this server has been successfully set to `{prefix}`");
                Log.Verbose($"Prefix for server {Context.Guild.Id} set to {prefix}");
            }
            catch (Exception e)
            {
                Log.Exception($"Setting prefix for {Context.Guild} ({Context.Guild.Id})", e);
                await ReplyAsync($"{CustomEmoji.Cross} There was a problem setting the prefix. {ContactMessage}");
            }
        }


        [Command("toggleprefix"), Alias("togglenoprefix"), Parameters("[channel id]")]
        [Remarks("Put a channel in \"No Prefix mode\"")]
        [Summary("When used by a user with the Manage Channels permission, toggles a channel between " +
                 "**Normal** mode and **No Prefix** mode.\nIn No Prefix mode, all commands will work without " +
                 "the need to use a prefix such as \"<\".\nIf the channel id is not specified, you will be" +
                 "asked to confirm the change for the current channel.\n\n" +
                 "**Warning!** This can result in a lot of unnecessary spam. Only use this if you're sure. " +
                 "It's a good idea only in dedicated channels, for example named \"#pacman\" or \"#botspam\"")]
        [PmRequireUserPermission(GuildPermission.ManageChannels)]
        public async Task ToggleNoPrefix(SocketGuildChannel otherChannel = null)
        {
            var channel = Context.Channel;
            if (otherChannel != null)
            {
                if (otherChannel is ISocketMessageChannel ch) channel = ch;
                else
                {
                    await ReplyAsync($"{otherChannel.Name} is not a text channel!");
                    return;
                }
            }

            try
            {
                if (!Storage.RequiresPrefix(channel))
                {
                    Storage.ToggleChannelGuildPrefix(channel.Id);
                    await ReplyAsync(
                        $"{CustomEmoji.Check} The {channel.Mention()} channel is back to **Normal mode** " +
                        $"(Prefix: `{Context.FixedPrefix}`)");
                    return;
                }

                await ReplyAsync(
                    $"❗__**Warning**__\nYou're about to set the channel {channel.Mention()} to **No Prefix mode**.\n" +
                    $"All commands will work without the need of a prefix such as `{Context.Prefix}`\n" +
                    $"This can lead to a lot of **unnecessary spam**. It's a good idea only in " +
                    $"dedicated bot or game channels.\n\n" +
                    $"Set {channel.Mention()} to *no prefix mode*? (Yes/No)");

                if (await GetYesResponseAsync(90) ?? false)
                {
                    Storage.ToggleChannelGuildPrefix(channel.Id);
                    await ReplyAsync(
                        $"{CustomEmoji.Check} The {channel.Mention()} channel is now in **No Prefix mode**. " +
                        $"All commands will work without any prefix.\nTo revert to normal, use `toggleprefix` again.");
                }
                else
                {
                    await ReplyAsync("Cancelled.");
                }
            }
            catch (Exception e)
            {
                Log.Exception($"Toggling prefix for {Context.Channel.FullName()}", e);
                await ReplyAsync($"{CustomEmoji.Cross} Oops, something went wrong. {ContactMessage}");
            }
        }
    }
}
