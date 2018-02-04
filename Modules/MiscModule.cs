using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Microsoft.Extensions.Configuration;
using PacManBot.Services;
using PacManBot.Constants;

namespace PacManBot.Modules
{
    [Name("📁Other")]
    public class MiscModule : ModuleBase<SocketCommandContext>
    {
        private readonly CommandService _commands;
        private readonly LoggingService _logger;
        private readonly IConfigurationRoot _config;

        public MiscModule(CommandService service, LoggingService logger, IConfigurationRoot config)
        {
            _commands = service;
            _logger = logger;
            _config = config;
        }

        [Command("help"), Alias("h"), Summary("List of commands")]
        public async Task HelpAsync([Remainder]string args = "") //Argument is useless for now
        {
            if (Context.Guild != null && !Context.BotHas(ChannelPermission.EmbedLinks))
            {
                await ReplyAsync("To show a fancy new help block, this bot requires the permission to Embed Links!");
                return;
            }

            var embed = new EmbedBuilder() { Color = new Color(241, 195, 15) }; //Create a new embed block
            embed.Title = $"{CustomEmoji.PacMan} **__List of commands__**";

            var allModules = _commands.Modules.OrderBy(m => m.Name); //Alphabetically
            foreach (var module in allModules) //Go through all modules
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
                            commandsText += $" {"- ".Unless(command.Summary.Contains("**-**"))}*{command.Summary}*";
                        }

                        commands.Add(command.Name);
                        commandsText += "\n";
                    }
                }

                if (!string.IsNullOrWhiteSpace(commandsText))
                {
                    embed.AddField(f =>
                    {
                        f.Name = $"{module.Name}";
                        f.Value = commandsText;
                        f.IsInline = false;
                    });
                }
            }

            string text = $"You can use the **{CommandHandler.ServerPrefix(Context.Guild) ?? _config["prefix"]}about** command for more info.";
            if (Context.Guild == null) text += "\nYou can also use commands without any prefix in a DM.";
            await ReplyAsync(text, false, embed.Build()); //Send the built embed
        }

        [Command("about"), Alias("info"), Summary("About this bot")]
        public async Task SayBotInfo()
        {
            if (Context.Guild != null && !Context.BotHas(ChannelPermission.EmbedLinks))
            {
                await ReplyAsync("To show a fancy new help block, this bot requires the permission to Embed Links!");
                return;
            }

            var embed = new EmbedBuilder() { Color = new Color(241, 195, 15) };
            embed.AddField("Server count", $"{Context.Client.Guilds.Count}", true);
            embed.AddField("Active games", $"{PacManModule.Game.gameInstances.Count}", true);
            embed.AddField("Latency", $"{Context.Client.Latency}ms", true);
            embed.AddField("Author", $"Samrux#3980", true);
            embed.AddField("Version", $"v2.4", true);
            embed.AddField("Prefix", CommandHandler.ServerPrefix(Context.Guild) ?? _config["prefix"], true);

            await ReplyAsync(File.ReadAllText(BotFile.About), false, embed.Build());
        }

        [Command("waka"), Alias("ping"), Summary("Waka waka waka")]
        public async Task Ping([Remainder]string args = "") //Useless args
        {
            var stopwatch = Stopwatch.StartNew();
            var message = await ReplyAsync($"{CustomEmoji.Loading} Waka");
            stopwatch.Stop();

            await message.ModifyAsync(m => m.Content = $"{CustomEmoji.PacMan} Waka in {(int)stopwatch.Elapsed.TotalMilliseconds}ms | {Context.Client.Guilds.Count} guilds | {PacManModule.Game.gameInstances.Count} active games\n");
        }

        [Command("say"), Summary("Make the bot say anything (Moderator)")]
        [RequireUserPermission(ChannelPermission.ManageMessages)]
        public async Task Say([Remainder]string text) => await ReplyAsync(text);

        [Command("clear"), Alias("c"), Summary("**[**amount**]** **-** Clear messages from this bot (Moderator)")]
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
                string prefix = CommandHandler.ServerPrefix(Context.Guild.Id) ?? _config["prefix"];
                reply = $"Prefix for this server is set to '{prefix}'{" (the default)".If(prefix == _config["prefix"])}. It can be changed with the command **setprefix**.";
            }
            await ReplyAsync(reply);
        }

        [Command("setprefix"), Summary("Set a custom prefix for this server (Admin)")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task SetServerPrefix(string newPrefix)
        {
            if (CommandHandler.prefixes.ContainsKey(Context.Guild.Id)) CommandHandler.prefixes[Context.Guild.Id] = newPrefix;
            else CommandHandler.prefixes.Add(Context.Guild.Id, newPrefix);

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

                await ReplyAsync($"{CustomEmoji.Check} Prefix for this server has been successfully set to '{newPrefix}'.");
                await _logger.Log(LogSeverity.Verbose, $"Prefix for server {Context.Guild.Name} set to {newPrefix}");
            }
            catch
            {
                await ReplyAsync($"{CustomEmoji.Cross} There was a problem storing the prefix on file. It might be reset the next time the bot restarts. Please try again or, if the problem persists, contact the bot author.");
                throw new Exception("Couldn't modify prefix on file");
            }
        }

        [Command("feedback"), Alias("bug"), Summary("Send the bot's developer a message")]
        public async Task SendFeedback([Remainder]string message)
        {
            try
            {
                File.AppendAllText(BotFile.FeedbackLog, $"\n\n[{Context.User.Username}#{Context.User.Discriminator}:] {message}");
                await ReplyAsync($"{CustomEmoji.Check} Message sent");
            }
            catch { await ReplyAsync("Oops, I didn't catch that. Please try again."); }
        }

        [Command("invite"), Alias("inv"), Summary("Invite this bot to your server")]
        public async Task SayBotInvite()
        {
            var embed = new EmbedBuilder() { Title = "Bot invite link", Color = new Color(241, 195, 15), ThumbnailUrl = Context.Client.CurrentUser.GetAvatarUrl(ImageFormat.Auto, 128)};
            embed.AddField($"➡ {File.ReadAllText(BotFile.Invite)}", "*Thanks for inviting Pac-Man Bot!*", false);
            await ReplyAsync("", false, embed.Build());
        }
    }
}
