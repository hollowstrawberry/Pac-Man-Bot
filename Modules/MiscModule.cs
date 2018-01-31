using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Microsoft.Extensions.Configuration;
using PacManBot.Services;
using System.Diagnostics;
using System.Threading;
using Discord.WebSocket;

namespace PacManBot.Modules
{
    [Name("Other")]
    public class MiscModule : ModuleBase<SocketCommandContext>
    {
        private readonly CommandService _service;
        private readonly IConfigurationRoot _config;

        public MiscModule(CommandService service, IConfigurationRoot config)
        {
            _service = service;
            _config = config;
        }


        [Command("help"), Alias("h"), Summary("List of commands")]
        public async Task HelpAsync([Remainder]string args = "") //Argument is useless for now
        {
            string prefix = _config["prefix"]; //Gets the prefix for the current server or uses the default one if not found
            if (Context.Guild != null && !CommandHandler.prefixes.TryGetValue(Context.Guild.Id, out prefix)) prefix = _config["prefix"];

            var embed = new EmbedBuilder() { Color = new Color(241, 195, 15) }; //Create a new embed block
            var allModules = _service.Modules.OrderBy(m => m.Name); //Alphabetically

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
                            commandsText += (i > 0 ? ", " : "") + $"**{command.Aliases[i]}**";
                        }
                        if (!string.IsNullOrEmpty(command.Summary)) //Adds the command summary
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
                        f.Name = module.Name;
                        f.Value = commandsText;
                        f.IsInline = false;
                    });
                }
            }


            string text = $"Command prefix for this server: **{prefix}**".Unless(prefix == _config["prefix"]); //Specifies the prefix if it's not the default one
            await ReplyAsync(text, false, embed.Build()); //Send the built embed
        }

        [Command("waka"), Alias("ping"), Summary("Waka.")]
        public async Task Ping([Remainder]string args = "") //Useless args
        {
            var stopwatch = Stopwatch.StartNew();
            var message = await ReplyAsync("Waka");
            stopwatch.Stop();

            await message.ModifyAsync(m => m.Content = $"Waka in {(int)stopwatch.Elapsed.TotalMilliseconds}ms | {Context.Client.Guilds.Count} guilds | {PacManModule.Game.gameInstances.Count} active games\n");
        }

        [Command("say"), Summary("Make the bot say anything (Moderator)")]
        [RequireUserPermission(ChannelPermission.ManageMessages)]
        public async Task Say([Remainder]string text) => await ReplyAsync(text);

        [Command("clear"), Alias("c"), Summary("Clear messages from this bot. You can specify amount (Moderator)")]
        [RequireUserPermission(ChannelPermission.ManageMessages)]
        public async Task ClearGameMessages(int amount = 10)
        {
            var messages = await Context.Channel.GetMessagesAsync(amount).FlattenAsync();
            foreach (IMessage message in messages)
            {
                if (message.Author.Id == Context.Client.CurrentUser.Id) await message.DeleteAsync(); //Remove all messages from this bot
            }
        }

        [Command("invite"), Alias("inv"), Summary("Invite this bot to your server")]
        public async Task SayBotInvite() => await ReplyAsync("Invite this bot into a server you own: <http://bit.ly/pacmanbotdiscord>");

        [Command("about"), Summary("About this bot")]
        public async Task SayBotInfo() => await ReplyAsync(File.ReadAllText(Program.File_About));


        [Command("setprefix"), Summary("Set a custom prefix for this server (Admin)")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task SetServerPrefix(string newPrefix)
        {
            if (CommandHandler.prefixes.ContainsKey(Context.Guild.Id)) CommandHandler.prefixes[Context.Guild.Id] = newPrefix;
            else CommandHandler.prefixes.Add(Context.Guild.Id, newPrefix);

            try
            {
                string file = Program.File_Prefixes;
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

                await ReplyAsync($"Prefix for this server has been successfully set to **{newPrefix}**.");
            }
            catch
            {
                await ReplyAsync($"There was a problem storing the prefix on file. It might be reset the next time the bot restarts.");
                throw new Exception("Couldn't modify prefix on file");
            }
        }
    }
}
