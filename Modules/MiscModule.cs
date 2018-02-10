using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using System.Threading.Tasks;
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


        [Command("about"), Alias("a", "info"), Remarks("— *About this bot*")]
        [Summary("Shows relevant information, data and links about Pac-Man Bot.")]
        public async Task SayBotInfo()
        {
            if (!Context.CheckHasEmbedPermission()) return;

            string[] file = File.ReadAllText(BotFile.About).Split("{links}");
            string description = file[0].Replace("{prefix}", storage.GetPrefixOrEmpty(Context.Guild));
            string[] links = file[1].Split('\n').Where(s => !string.IsNullOrWhiteSpace(s.Trim(' ', '\n'))).ToArray();

            var embed = new EmbedBuilder()
            {
                Title = $"{CustomEmojis.PacMan} __**Pac-Man Bot**__",
                Description = description,
                Color = new Color(241, 195, 15)
            };
            embed.AddField("Server count", $"{Context.Client.Guilds.Count}", true);
            embed.AddField("Active games", $"{storage.gameInstances.Count}", true);
            embed.AddField("Latency", $"{Context.Client.Latency}ms", true);
            embed.AddField("Author", $"Samrux#3980", true);
            embed.AddField("Version", $"v2.9", true);
            embed.AddField("Library", "Discord.Net 2.0 (C#)", true);

            for (int i = 0; i < links.Length; i++)
            {
                embed.AddField(links[i].Split('|')[0], $"[Click here]({links[i].Split('|')[1]} \"{links[i].Split('|')[1]}\")", true);
            }

            await ReplyAsync("", false, embed.Build());
        }

        [Command("help"), Alias("h", "commands"), Remarks("[command] — *List of commands or help about a command*")]
        [Summary("Show a complete list of commands you can use. You can specify a command to see detailed help about that command.")]
        public async Task SendCommandHelp(string commandName) //With argument
        {
            if (!Context.CheckHasEmbedPermission()) return;

            string prefix = storage.GetPrefix(Context.Guild).If(Context.Guild != null);

            CommandInfo command;
            try { command = commands.Commands.First(c => c.Aliases.Contains(commandName)); }
            catch
            {
                await ReplyAsync($"Can't find a command with that name. Use **{prefix}help** for a list of commands.");
                return;
            }

            var embed = new EmbedBuilder()
            {
                Title = $"{CustomEmojis.PacMan} __Command__: {prefix}{command.Name}",
                Color = new Color(241, 195, 15)
            };

            string parameters = string.IsNullOrWhiteSpace(command.Remarks) ? "" : command.Remarks.Split('—')[0].Trim();
            if (!string.IsNullOrWhiteSpace(parameters))
            {
                embed.AddField("Parameters", parameters, true);
            }
            if (command.Aliases.Count > 1)
            {
                string aliasList = "";
                for (int i = 1; i < command.Aliases.Count; i++) aliasList += $"{", ".If(i > 1)}{prefix}{command.Aliases[i]}";
                embed.AddField("Aliases", aliasList, true);
            }
            if (!string.IsNullOrWhiteSpace(command.Summary))
            {
                string summary = command.Summary.Replace("{prefix}", prefix);
                if (!string.IsNullOrWhiteSpace(parameters) && parameters.Contains("[")) summary += "\n\n*Parameters in [brackets] are optional.*";
                embed.AddField("Summary", summary, false);
            }

            await ReplyAsync("", false, embed.Build()); //Send the built embed
        }

        [Command("help"), Alias("h", "commands")]
        public async Task SendAllHelp() //Without arguments
        {
            if (!Context.CheckHasEmbedPermission()) return;

            string prefix = storage.GetPrefix(Context.Guild).If(Context.Guild != null);

            var embed = new EmbedBuilder()
            {
                Title = $"{CustomEmojis.PacMan} __**Bot Commands**__",
                Description = (Context.Guild == null ? "No prefix is needed in a DM!" : $"Prefix for this server is '{prefix}'") + $"\nYou can do **{prefix}help command** for more information about a command.\n*Aliases are listed with commas. Parameters with [brackets] are optional.*",
                Color = new Color(241, 195, 15)
            };

            var allModules = commands.Modules.OrderBy(m => m.Name); //Alphabetically
            foreach (var module in allModules.Where(m => !m.Preconditions.Contains(new RequireOwnerAttribute()))) //Go through all modules except dev modules
            {
                string moduleText = null; //Text under the module title in the embed block
                List<string> oldCommands = new List<string>(); //Storing the command names so they can't repeat

                foreach (var command in module.Commands) //Go through all commands
                {
                    var canUse = await command.CheckPreconditionsAsync(Context); //Only show commands the user can use
                    if (canUse.IsSuccess && !oldCommands.Contains(command.Name))
                    {
                        for (int i = 0; i < command.Aliases.Count; i++) //Lists command name and aliases
                        {
                            moduleText += $"{", ".If(i > 0)}**{command.Aliases[i]}**";
                        }

                        if (!string.IsNullOrWhiteSpace(command.Remarks)) moduleText += $" {command.Remarks}"; //Short description
                        moduleText += "\n";

                        oldCommands.Add(command.Name);
                    }
                }

                if (!string.IsNullOrWhiteSpace(moduleText)) embed.AddField($"{module.Name}", moduleText, false);
            }

            await ReplyAsync("", false, embed.Build()); //Send the built embed
        }

        [Command("waka"), Alias("ping"), Remarks("— *Waka waka waka*")]
        [Summary("Tests the ping (server reaction time in milliseconds) and shows other quick stats about the bot at the current moment.\nDid you know the bot responds every time you say \"waka\" in chat? Shhh, it's a secret.")]
        public async Task Ping([Remainder]string args = "") //Useless args
        {
            var stopwatch = Stopwatch.StartNew();
            var message = await ReplyAsync($"{CustomEmojis.Loading} Waka");
            stopwatch.Stop();

            await message.ModifyAsync(m => m.Content = $"{CustomEmojis.PacMan} Waka in {(int)stopwatch.Elapsed.TotalMilliseconds}ms | {Context.Client.Guilds.Count} guilds | {storage.gameInstances.Count} active games\n");
        }

        [Command("prefix"), Remarks("— *Show the current prefix for this server*")]
        [Summary("Reminds you of this bot's prefix for this server. Tip: The prefix is already here in this help block.\nYou can use the **{prefix}setprefix prefix** command to set a prefix if you're an Administrator.")]
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

        [Command("feedback"), Alias("suggestion", "bug"), Remarks("message — *Send a message to the bot's developer*")]
        [Summary("Whatever text you write after this command will be sent directly to the bot's developer. You may receive an answer through the bot in a DM.")]
        public async Task SendFeedback([Remainder]string message)
        {
            try
            {
                File.AppendAllText(BotFile.FeedbackLog, $"[{Context.User.FullName()} {Context.User.Id}] {message}\n\n");
                await ReplyAsync($"{CustomEmojis.Check} Message sent. Thank you!");
                await (await Context.Client.GetApplicationInfoAsync()).Owner.SendMessageAsync($"```diff\n+Feedback received: {Context.User.FullName()} {Context.User.Id}```\n{message}");
            }
            catch { await ReplyAsync("Oops, I didn't catch that. Please try again."); }
        }

        [Command("invite"), Alias("inv"), Remarks("— *Invite this bot to your server*")]
        [Summary("Shows a fancy embed block with the bot's invite link. I'd show it right now too, since you're already here, but I really want you to see that fancy embed.")]
        public async Task SayBotInvite([Remainder]string args = "") //Useless args
        {
            if (!Context.CheckHasEmbedPermission()) return;

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
