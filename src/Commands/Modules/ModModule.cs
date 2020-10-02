using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using PacManBot.Constants;
using PacManBot.Extensions;

namespace PacManBot.Commands.Modules
{
    [Module(ModuleNames.Mod)]
    [RequireBotPermissions(BaseBotPermissions)]
    public class ModModule : BasePmBotModule
    {
        string ContactMessage(CommandContext ctx) =>
            $"Please try again or, if the problem persists, contact the bot owner using `{Storage.GetPrefix(ctx)}feedback`.";


        [Command("say")]
        [Description("Repeats back the message provided.")]
        [RequireUserPermissions(Permissions.ManageMessages)]
        public Task Say(CommandContext ctx, [RemainingText] string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return ctx.RespondAsync("Did you want me to say something?");
            return ctx.RespondAsync(message.SanitizeMentions());
        }


        [Command("clear"), Aliases("clean")]
        [Description("Clears all commands and messages for *this bot only*, by default the last 10 commands.")]
        [RequireGuild, RequireUserPermissions(Permissions.ManageMessages)]
        public async Task ClearCommandMessages(CommandContext ctx, int amount = 10)
        {
            if (amount < 1 || amount > 100)
            {
                await ctx.RespondAsync("Please choose a reasonable number of messages to delete.");
                return;
            }

            IEnumerable<DiscordMessage> toDelete = await ctx.Channel.GetMessagesAsync();

            if (ctx.BotCan(Permissions.ManageMessages))
            {
                toDelete = toDelete.Where(x => x.Author.Id == ctx.Client.CurrentUser.Id
                    || x.Content.StartsWith(Storage.GetGuildPrefix(ctx.Guild))
                    || Input.MentionPrefix.IsMatch(x.Content));
            }
            else toDelete = toDelete.Where(x => x.Author.Id == ctx.Client.CurrentUser.Id);

            toDelete = toDelete.Take(amount);

            if (toDelete.Count() > 0)
            {
                try
                {
                    await ctx.Channel.DeleteMessagesAsync(toDelete);
                }
                catch (Exception e)
                {
                    Log.Exception($"Couldn't delete messages in {ctx.Channel.DebugName()}", e);
                }
            }
        }


        [Command("setprefix")]
        [Description("Change the custom prefix for this server.\n" +
        "Prefixes can't contain these characters: \\* \\_ \\~ \\` \\\\")]
        [RequireGuild, RequireUserPermissions(Permissions.ManageGuild)]
        public async Task SetServerPrefix(CommandContext ctx, string prefix)
        {
            string error = null;
            if (string.IsNullOrWhiteSpace(prefix))
                error = $"The guild prefix can't be empty. If you don't want a prefix in a channel, check out `{ctx.Prefix}toggleprefix`";
            else if (prefix.ContainsAny('*', '_', '~', '`', '\''))
                error = "The prefix can't contain markdown special characters: *_~\\`\\\\";
            else if (prefix.Contains("||"))
                error = "The prefix can't contain \"||\"";
            else if (prefix.Length > 32)
                error = "Prefix can't be bigger than 32 characters";

            if (error != null)
            {
                await ctx.RespondAsync($"{CustomEmoji.Cross} {error}");
                return;
            }


            try
            {
                Storage.SetGuildPrefix(ctx.Guild.Id, prefix);
                await ctx.RespondAsync($"{CustomEmoji.Check} Prefix for this server has been successfully set to `{prefix}`");
                Log.Debug($"Prefix for server {ctx.Guild.Id} set to {prefix}");
            }
            catch (Exception e)
            {
                Log.Exception($"Setting prefix for {ctx.Guild} ({ctx.Guild.Id})", e);
                await ctx.RespondAsync($"{CustomEmoji.Cross} There was a problem setting the prefix. {ContactMessage(ctx)}");
            }
        }


        [Command("toggleprefix"), Aliases("togglenoprefix")]
        [Description("Toggles a channel between " +
        "**Normal** mode and **No Prefix** mode.\nIn No Prefix mode, all commands will work without " +
        "the need to use a prefix such as \"<\".\nIf the channel id is not specified, you will be" +
        "asked to confirm the change for the current channel.\n\n" +
        "**Warning!** This can result in a lot of unnecessary spam. Only use this if you're sure. " +
        "It's a good idea only in dedicated channels, for example named \"#pacman\" or \"#botspam\"")]
        [RequireGuild, RequireUserPermissions(Permissions.ManageChannels)]
        public async Task ToggleNoPrefix(CommandContext ctx, DiscordChannel otherChannel = null)
        {
            var channel = ctx.Channel;
            if (otherChannel != null)
            {
                if (otherChannel.Type == ChannelType.Text) channel = otherChannel;
                else
                {
                    await ctx.RespondAsync($"{otherChannel.Name} is not a text channel!");
                    return;
                }
            }

            try
            {
                if (!Storage.RequiresPrefix(channel))
                {
                    Storage.ToggleChannelGuildPrefix(channel.Id);
                    await ctx.RespondAsync(
                        $"{CustomEmoji.Check} The {channel.Mention} channel is back to **Normal mode** " +
                        $"(Prefix: `{Storage.GetGuildPrefix(ctx.Guild)}`)");
                    return;
                }

                await ctx.RespondAsync(
                    $"❗__**Warning**__\nYou're about to set the channel {channel.Mention} to **No Prefix mode**.\n" +
                    $"All commands will work without the need of a prefix such as `{Storage.GetPrefix(ctx)}`\n" +
                    $"This can lead to a lot of **unnecessary spam**. It's a good idea only in " +
                    $"dedicated bot or game channels.\n\n" +
                    $"Set {channel.Mention} to *no prefix mode*? (Yes/No)");

                if (await ctx.GetYesResponseAsync(90) ?? false)
                {
                    Storage.ToggleChannelGuildPrefix(channel.Id);
                    await ctx.RespondAsync(
                        $"{CustomEmoji.Check} The {channel.Mention} channel is now in **No Prefix mode**. " +
                        $"All commands will work without any prefix.\nTo revert to normal, use `toggleprefix` again.");
                }
                else
                {
                    await ctx.RespondAsync("Cancelled.");
                }
            }
            catch (Exception e)
            {
                Log.Exception($"Toggling prefix for {ctx.Channel.DebugName()}", e);
                await ctx.RespondAsync($"{CustomEmoji.Cross} Oops, something went wrong. {ContactMessage(ctx)}");
            }
        }


        [Command("dev"), Aliases("devcommands")]
        [RequireOwner]
        [Description("Lists developer commands.")]
        public async Task ShowDevCommands(CommandContext ctx)
        {
            var commands = typeof(OwnerModule).GetMethods()
                .Select(x => x.Get<CommandAttribute>()?.Name)
                .Where(x => x != null)
                .Distinct()
                .Select(x => $"`{x}`")
                .JoinString(", ");

            var embed = new DiscordEmbedBuilder
            {
                Title = $"{CustomEmoji.Staff} Developer Commands",
                Color = Colors.PacManYellow,
                Description = commands
            };

            await ctx.RespondAsync(embed);
        }
    }
}
