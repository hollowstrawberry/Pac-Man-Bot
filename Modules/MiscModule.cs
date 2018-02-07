using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Scripting;
using Discord;
using Discord.Commands;
using PacManBot.Services;
using PacManBot.Constants;

namespace PacManBot.Modules
{
    [Name("📁Other")]
    public class MiscModule : ModuleBase<SocketCommandContext>
    {
        private readonly CommandService commands;
        private readonly LoggingService logger;
        private readonly StorageService storage;

        public MiscModule(CommandService commands, LoggingService logger, StorageService storage)
        {
            this.commands = commands;
            this.logger = logger;
            this.storage = storage;
        }

        [Command("help"), Alias("h", "commands"), Summary("List of commands")]
        public async Task HelpAsync([Remainder]string args = "") //Argument is useless for now
        {
            if (Context.Guild != null && !Context.BotHas(ChannelPermission.EmbedLinks))
            {
                await ReplyAsync("To show a fancy new help block, this bot requires the permission to Embed Links!");
                return;
            }

            string prefix = storage.GetPrefix(Context.Guild).If(Context.Guild != null);

            var embed = new EmbedBuilder()
            {
                Title = $"{CustomEmojis.PacMan} __**Bot Commands**__",
                Description = $"Prefix for this server is '{prefix}'\n".Unless(prefix == "") + $"You can use the **{prefix}about** command for more information" + "\nNo prefix is necessary in a DM!".If(Context.Guild == null),
                Color = new Color(241, 195, 15)
            };

            var allModules = commands.Modules.OrderBy(m => m.Name); //Alphabetically
            foreach (var module in allModules.Where(m => !m.Preconditions.Contains(new RequireOwnerAttribute()))) //Go through all modules except dev modules
            {
                string commandsText = null; //Text under the module title in the embed block
                List<string> commands = new List<string>(); //Storing the command names so they can't repeat

                foreach (var command in module.Commands) //Go through all commands
                {
                    var canUse = await command.CheckPreconditionsAsync(Context); //Only show commands the user can use
                    if (canUse.IsSuccess && !commands.Contains(command.Name))
                    {
                        for (int i = 0; i < command.Aliases.Count; i++) //Lists command name and aliases
                        {
                            commandsText += $"{", ".If(i > 0)}**{command.Aliases[i]}**";
                        }
                        if (!string.IsNullOrWhiteSpace(command.Summary)) //Adds the command summary
                        {
                            commandsText += $" {command.Remarks} — *{command.Summary}*";
                        }

                        commands.Add(command.Name);
                        commandsText += "\n";
                    }
                }

                if (!string.IsNullOrWhiteSpace(commandsText)) embed.AddField($"{module.Name}", commandsText, false);
            }

            string text = "";
            await ReplyAsync(text, false, embed.Build()); //Send the built embed
        }

        [Command("about"), Alias("a", "info"), Summary("About this bot")]
        public async Task SayBotInfo()
        {
            if (Context.Guild != null && !Context.BotHas(ChannelPermission.EmbedLinks))
            {
                await ReplyAsync("To show a fancy new help block, this bot requires the permission to Embed Links!");
                return;
            }

            var embed = new EmbedBuilder()
            {
                Title = $"{CustomEmojis.PacMan} __**Pac-Man Bot**__",
                Description = File.ReadAllText(BotFile.About).Replace("{prefix}", storage.GetPrefixOrEmpty(Context.Guild)),
                 Color = new Color(241, 195, 15)
            };
            embed.AddField("Server count", $"{Context.Client.Guilds.Count}", true);
            embed.AddField("Active games", $"{storage.gameInstances.Count}", true);
            embed.AddField("Latency", $"{Context.Client.Latency}ms", true);
            embed.AddField("Author", $"Samrux#3980", true);
            embed.AddField("Version", $"v2.6", true);
            embed.AddField("Library", "Discord.Net 2.0 (C#)", true);
            embed.AddField($"{CustomEmojis.Discord} Bot invite link", $"[Click here]({File.ReadAllText(BotFile.InviteLink)} \"{File.ReadAllText(BotFile.InviteLink)}\")", true);
            embed.AddField($"{CustomEmojis.GitHub} Source code", $"[Click here](https://github.com/Samrux/Pac-Man-Bot \"https://github.com/Samrux/Pac-Man-Bot\")", true);

            await ReplyAsync("", false, embed.Build());
        }

        [Command("waka"), Alias("ping"), Summary("Waka waka waka")]
        public async Task Ping([Remainder]string args = "") //Useless args
        {
            var stopwatch = Stopwatch.StartNew();
            var message = await ReplyAsync($"{CustomEmojis.Loading} Waka");
            stopwatch.Stop();

            await message.ModifyAsync(m => m.Content = $"{CustomEmojis.PacMan} Waka in {(int)stopwatch.Elapsed.TotalMilliseconds}ms | {Context.Client.Guilds.Count} guilds | {storage.gameInstances.Count} active games\n");
        }

        [Command("say"), Remarks("message"), Summary("Make the bot say anything (Moderator)")]
        [RequireUserPermission(ChannelPermission.ManageMessages)]
        public async Task Say([Remainder]string text) => await ReplyAsync(text);

        [Command("clear"), Alias("c"), Remarks("[amount]"), Summary("Clear messages from this bot (Moderator)")]
        [RequireUserPermission(ChannelPermission.ManageMessages)]
        public async Task ClearGameMessages(int amount = 10)
        {
            var messages = await Context.Channel.GetMessagesAsync(amount).FlattenAsync();
            foreach (IMessage message in messages)
            {
                if (message.Author.Id == Context.Client.CurrentUser.Id) await message.DeleteAsync(); //Remove all messages from this bot
            }
        }

        [Command("prefix"), Summary("Show the current prefix for this server")]
        public async Task GetServerPrefix([Remainder]string args = "") //Useless args
        {
            string reply;
            if (Context.Guild == null)
            {
                reply = "You can use commands without any prefix in a DM with me!";
            }
            else
            {
                string prefix = storage.GetPrefix(Context.Guild.Id);
                reply = $"Prefix for this server is set to '{prefix}'{" (the default)".If(prefix == storage.defaultPrefix)}. It can be changed with the command **setprefix**.";
            }
            await ReplyAsync(reply);
        }

        [Command("setprefix"), Remarks("prefix"), Summary("Set a custom prefix for this server (Admin)")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task SetServerPrefix(string newPrefix)
        {
            if (storage.prefixes.ContainsKey(Context.Guild.Id)) storage.prefixes[Context.Guild.Id] = newPrefix;
            else storage.prefixes.Add(Context.Guild.Id, newPrefix);

            try
            {
                string file = BotFile.Prefixes;
                string[] lines = File.ReadAllLines(file);

                int prefixIndex = lines.Length; //After everything else by default
                for (int i = 0; i < lines.Length; i++) if (lines[i].Split(' ')[0] == Context.Guild.Id.ToString()) prefixIndex = i; //Finds if the server already has a custom prefix

                string newLine = $"{Context.Guild.Id} {newPrefix}";
                if (prefixIndex >= lines.Length) //Outside the array
                {
                    File.AppendAllLines(file, new string[] { newLine });
                }
                else //Existing line
                {
                    lines[prefixIndex] = newLine;
                    File.WriteAllLines(file, lines);
                }

                await ReplyAsync($"{CustomEmojis.Check} Prefix for this server has been successfully set to '{newPrefix}'.");
                await logger.Log(LogSeverity.Verbose, $"Prefix for server {Context.Guild.Name} set to {newPrefix}");
            }
            catch
            {
                string prefix = storage.GetPrefixOrEmpty(Context.Guild);
                await ReplyAsync($"{CustomEmojis.Cross} There was a problem storing the prefix on file. It might be reset the next time the bot restarts. Please try again or, if the problem persists, contact the bot author using **{prefix}feedback**.");
                throw new Exception("Couldn't modify prefix on file");
            }
        }

        [Command("feedback"), Alias("suggestion", "bug"), Remarks("message"), Summary("Send a message to the bot's developer")]
        public async Task SendFeedback([Remainder]string message)
        {
            try
            {
                File.AppendAllText(BotFile.FeedbackLog, $"[{Context.User.FullName()} {Context.User.Id}] {message}\n\n");
                await ReplyAsync($"{CustomEmojis.Check} Message sent. Thank you!");
            }
            catch { await ReplyAsync("Oops, I didn't catch that. Please try again."); }
        }

        [Command("invite"), Alias("inv"), Summary("Invite this bot to your server")]
        public async Task SayBotInvite()
        {
            string link = File.ReadAllText(BotFile.InviteLink);
            var embed = new EmbedBuilder()
            {
                Title = "Bot invite link",
                Color = new Color(241, 195, 15),
                ThumbnailUrl = Context.Client.CurrentUser.GetAvatarUrl(ImageFormat.Auto, 128)
            };
            embed.AddField($"➡ <{link}>", "*Thanks for inviting Pac-Man Bot!*", false);
            await ReplyAsync("", false, embed.Build());
        }
    }
}
