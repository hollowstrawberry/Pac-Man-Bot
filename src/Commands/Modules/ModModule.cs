using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Net;
using Discord.Commands;
using Discord.WebSocket;
using PacManBot.Constants;
using PacManBot.Extensions;

namespace PacManBot.Commands
{
    [Name(CustomEmoji.Staff + "Mod"), Remarks("5")]
    [BetterRequireUserPermission(GuildPermission.ManageMessages)]
    public class ModModule : BaseCustomModule
    {
        public ModModule(IServiceProvider services) : base(services) { }


        string ContactMessage => $"Please try again or, if the problem persists, contact the bot author using `{Prefix}feedback`.";


        [Command("say"), Remarks("Make the bot say anything")]
        [Summary("Repeats back the message provided. Only users with the Manage Messages permission can use this command.")]
        public async Task Say([Remainder]string message)
            => await ReplyAsync(message.SanitizeMentions());


        [Command("clear"), Alias("clean", "cl"), Remarks("Clear this bot's messages and commands")]
        [Summary("Clears all commands and messages for *this bot only*, from the last [amount] messages, " +
                 "or the last 10 messages by default.\nOnly users with the Manage Messages permission can use this command.")]
        [BetterRequireBotPermission(ChannelPermission.ReadMessageHistory)]
        public async Task ClearCommandMessages(int amount = 10)
        {
            int _ = 0;
            bool canDelete = Context.BotCan(ChannelPermission.ManageMessages);

            var messages = (await Context.Channel.GetMessagesAsync(amount).FlattenAsync())
                .Select(x => x as IUserMessage).Where(x => x != null)
                .Where(x => x.Author.Id == Context.Client.CurrentUser.Id
                       || canDelete &&
                          (x.Content.StartsWith(AbsolutePrefix) && !x.Content.StartsWith("<@")
                           || x.HasMentionPrefix(Context.Client.CurrentUser, ref _)));

            foreach (var message in messages)
            {
                try
                {
                    await message.DeleteAsync(DefaultOptions);
                }
                catch (Exception e) when (e is HttpException || e is TimeoutException)
                {
                    await Logger.Log(LogSeverity.Warning,
                                     $"Couldn't delete message {message.Id} in {Context.Channel.FullName()}: {e.Message}");
                }
            }
        }


        [Command("setprefix"), Remarks("Set a custom prefix for this server")]
        [Summary("Change the custom prefix for this server. Requires the user to have the Manage Guild permission.\n" +
                 "Prefixes can't contain these characters: \\* \\_ \\~ \\` \\\\")]
        [BetterRequireUserPermission(GuildPermission.ManageGuild)]
        public async Task SetServerPrefix(string prefix)
        {
            string error = null;
            if (string.IsNullOrWhiteSpace(prefix))
                error = $"The guild prefix can't be empty. If you don't want a prefix in a channel, check out `{Prefix}togglenoprefix`";
            else if (prefix.ContainsAny("*", "_", "~", "`", "\\"))
                error = "The prefix can't contain markdown special characters: *_~\\`\\\\";
            else if (prefix.Length > 32)
                error = "Prefix can't be bigger than 32 characters";

            if (error != null)
            {
                await ReplyAsync($"{CustomEmoji.Cross} {error}");
                return;
            }


            try
            {
                Storage.SetPrefix(Context.Guild.Id, prefix);
                await ReplyAsync($"{CustomEmoji.Check} Prefix for this server has been successfully set to `{prefix}`");
                await Logger.Log(LogSeverity.Info, $"Prefix for server {Context.Guild.Id} set to {prefix}");
            }
            catch (Exception)
            {
                await ReplyAsync($"{CustomEmoji.Cross} There was a problem setting the prefix. {ContactMessage}");
                throw;
            }
        }


        [Command("togglewaka"), Remarks("Turn off bot autoresponses in the server")]
        [Summary("The bot normally responds every time a message contains purely multiples of \"waka\", " +
                 "unless it's turned off server-wide using this command. Requires the user to have the Manage Guild permission.")]
        [BetterRequireUserPermission(GuildPermission.ManageGuild)]
        public async Task ToggleWakaResponse()
        {
            try
            {
                bool waka = Storage.ToggleAutoresponse(Context.Guild.Id);
                await ReplyAsync($"{CustomEmoji.Check} Autoresponses turned **{(waka ? "ON" : "OFF")}** in this server.");
                await Logger.Log(LogSeverity.Info, $"Autoresponses turned {(waka ? "on" : "off")} in {Context.Guild.Id}");
            }
            catch (Exception)
            {
                await ReplyAsync($"{CustomEmoji.Cross} Oops, something went wrong. {ContactMessage}");
                throw;
            }
        }


        [Command("togglenoprefix"), Alias("toggleprefix"), Parameters("[channel id]")]
        [Remarks("Put a channel in \"No Prefix mode\"")]
        [Summary("When used by a user with the Manage Channels permission, toggles a channel between " +
                 "**Normal** mode and **No Prefix** mode.\nIn No Prefix mode, all commands will work without " +
                 "the need to use a prefix such as \"<\".\nIf the channel id is not specified, you will be" +
                 "asked to confirm the change for the current channel.\n\n" +
                 "**Warning!** This can result in a lot of unnecessary spam. Only use this if you're sure. " +
                 "It's a good idea only in dedicated channels, for example named \"#pacman\" or \"#botspam\"")]
        [BetterRequireUserPermission(GuildPermission.ManageChannels)]
        public async Task ToggleNoPrefix([Remainder]string arg = "")
        {
            bool specified = ulong.TryParse(arg, out ulong channelId) && Context.Guild.TextChannels.Any(x => x.Id == channelId);
            if (!specified) channelId = Context.Channel.Id;

            var channel = specified ? Context.Guild.GetTextChannel(channelId) : (SocketTextChannel)Context.Channel;

            try
            {
                if (Storage.NoPrefixChannel(channelId))
                {
                    Storage.ToggleNoPrefix(channelId);
                    await ReplyAsync(
                        $"{CustomEmoji.Check} The {channel.Mention} channel is back to **Normal mode** " +
                        $"(Prefix: `{AbsolutePrefix}`)");
                }
                else
                {
                    if (specified)
                    {
                        Storage.ToggleNoPrefix(channelId);
                        await ReplyAsync(
                            $"{CustomEmoji.Check} The {channel.Mention} channel is now in **No Prefix mode**. " +
                            $"All commands will work without any prefix.\nTo revert to normal, use `togglenoprefix` again.");
                    }
                    else
                    {
                        await ReplyAsync(
                            $"❗__**Warning**__\nYou're about to set this channel to **No Prefix mode**.\n" +
                            $"All commands will work without the need of a prefix such as `{Prefix}`\n" +
                            $"This can lead to a lot of *unnecessary spam*. It's a good idea only in " +
                            $"dedicated channels, for example named \"#pacman\" or \"#botspam\".\n\n" +
                            $"To set this channel to No Prefix mode, please do `{Prefix}togglenoprefix {channelId}`");
                    }
                }
            }
            catch (Exception)
            {
                await ReplyAsync($"{CustomEmoji.Cross} Oops, something went wrong. {ContactMessage}");
                throw;
            }
        }
    }
}
