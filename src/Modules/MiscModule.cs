using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using PacManBot.Services;
using PacManBot.Constants;

namespace PacManBot.Modules
{
    [Name("📁Other")]
    public class MiscModule : ModuleBase<SocketCommandContext>
    {
        private readonly DiscordShardedClient shardedClient;
        private readonly CommandService commands;
        private readonly LoggingService logger;
        private readonly StorageService storage;


        public MiscModule(DiscordShardedClient shardedClient, CommandService commands, LoggingService logger, StorageService storage)
        {
            this.shardedClient = shardedClient;
            this.commands = commands;
            this.logger = logger;
            this.storage = storage;
        }



        [Command("about"), Alias("a", "info"), Remarks("About this bot")]
        [Summary("Shows relevant information, data and links about Pac-Man Bot.")]
        [RequireBotPermissionImproved(ChannelPermission.EmbedLinks)]
        public async Task SendBotInfo()
        {
            string description = storage.BotContent["about"].Replace("{prefix}", storage.GetPrefixOrEmpty(Context.Guild));
            string[] fields = storage.BotContent["aboutfields"].Split('\n').Where(s => s.Contains("|")).ToArray();

            var embed = new EmbedBuilder()
            {
                Title = $"{CustomEmoji.PacMan} __**Pac-Man Bot**__",
                Description = description,
                Color = new Color(241, 195, 15)
            };
            embed.AddField("Total guilds", $"{shardedClient.Guilds.Count}", true);
            embed.AddField("Total active games", $"{storage.GameInstances.Count}", true);
            embed.AddField("Latency", $"{Context.Client.Latency}ms", true);

            for (int i = 0; i < fields.Length; i++)
            {
                string[] splice = fields[i].Split('|');
                embed.AddField(splice[0], splice[1], true);
            }

            await ReplyAsync("", false, embed.Build());
        }


        [Command("help"), Alias("h", "commands"), Parameters("[command]"), Remarks("List of commands or help about a command")]
        [Summary("Show a complete list of commands you can use. You can specify a command to see detailed help about that command.")]
        [RequireBotPermissionImproved(ChannelPermission.EmbedLinks)]
        public async Task SendCommandHelp(string commandName)
        {
            string prefix = storage.GetPrefixOrEmpty(Context.Guild);

            CommandInfo command = commands.Commands.FirstOrDefault(c => c.Aliases.Contains(commandName));
            if (command == null)
            {
                await ReplyAsync($"Can't find a command with that name. Use **{prefix}help** for a list of commands.");
                return;
            }

            var helpInfo = new CommandHelpInfo(command);


            var embed = new EmbedBuilder()
            {
                Title = $"{CustomEmoji.PacMan} __Command__: {prefix}{command.Name}",
                Color = new Color(241, 195, 15)
            };

            if (helpInfo.Hidden) embed.AddField("Hidden command", "*Are you a wizard?*", true);

            if (helpInfo.Parameters != "") embed.AddField("Parameters", helpInfo.Parameters, true);

            if (command.Aliases.Count > 1)
            {
                string aliasList = "";
                for (int i = 1; i < command.Aliases.Count; i++) aliasList += $"{", ".If(i > 1)}{prefix}{command.Aliases[i]}";
                embed.AddField("Aliases", aliasList, true);
            }

            if (helpInfo.Summary != "") embed.AddField("Summary", helpInfo.Summary.Replace("{prefix}", prefix), false);

            if (helpInfo.ExampleUsage != "") embed.AddField("Example Usage", helpInfo.ExampleUsage.Replace("{prefix}", prefix), false);

            await ReplyAsync("", false, embed.Build());
        }


        [Command("help"), Alias("h", "commands")]
        [RequireBotPermissionImproved(ChannelPermission.EmbedLinks)]
        public async Task SendAllHelp()
        {
            string prefix = storage.GetPrefix(Context.Guild).If(Context.Guild != null);

            var embed = new EmbedBuilder()
            {
                Title = $"{CustomEmoji.PacMan} __**Bot Commands**__",
                Description = (Context.Guild == null ? "No prefix is needed in a DM!" : $"Prefix for this server is '{prefix}'")
                            + $"\nYou can do **{prefix}help command** for more information about a command.\n*Parameter types: <needed> [optional]*",
                Color = new Color(241, 195, 15)
            };

            foreach (var module in commands.Modules.OrderBy(m => m.Name))
            {
                var moduleText = new StringBuilder(); //Text under the module title in the embed block
                List<string> previousCommands = new List<string>(); //Storing the command names so they can't repeat

                foreach (var command in module.Commands) //Go through all commands
                {
                    var helpInfo = new CommandHelpInfo(command);

                    if (!helpInfo.Hidden && !previousCommands.Contains(command.Name)
                        && (await command.CheckPreconditionsAsync(Context)).IsSuccess //Only shows commands which can be executed
                    ) {
                        moduleText.Append($"**{command.Name} {helpInfo.Parameters}**");
                        if (helpInfo.Remarks != "") moduleText.Append($" — *{helpInfo.Remarks}*");
                        moduleText.Append('\n');

                        previousCommands.Add(command.Name);
                    }
                }

                if (moduleText.Length > 0) embed.AddField(module.Name, moduleText.ToString(), false);
            }

            await ReplyAsync("", false, embed.Build()); //Send the built embed
        }


        [Command("waka"), Alias("ping"), Parameters(""), Remarks("Ping? Nah, waka.")]
        [Summary("Tests the ping (server reaction time in milliseconds) and shows other quick stats about the bot at the current moment.\n" +
                 "Did you know the bot responds every time you say \"waka\" in chat? Shhh, it's a secret.")]
        public async Task Ping([Remainder]string args = "") //Useless args
        {
            var stopwatch = Stopwatch.StartNew(); // Measure the time it takes to send a message to chat
            var message = await ReplyAsync($"{CustomEmoji.Loading} Waka", options: Utils.DefaultRequestOptions);
            stopwatch.Stop();

            int shardGames = 0;
            foreach (var game in storage.GameInstances)
            {
                if (game.Guild != null && Context.Client.Guilds.Contains(game.Guild) || game.Guild == null && Context.Client.ShardId == 0)
                {
                    shardGames++;
                }
            }

            string content = $"{CustomEmoji.PacMan} Waka in `{(int)stopwatch.ElapsedMilliseconds}`ms **|** {shardedClient.Guilds.Count} total guilds, {storage.GameInstances.Count} total active games";
            if (shardedClient.Shards.Count > 1) content += $"```css\nShard {Context.Client.ShardId + 1}/{shardedClient.Shards.Count} controlling {Context.Client.Guilds.Count} guilds and {shardGames} games```";
            await message.ModifyAsync(m => m.Content = content, Utils.DefaultRequestOptions);                   
        }


        [Command("prefix"), Remarks("Show the current prefix for this server")]
        [Summary("Shows this bot's prefix for this server, even though you can already see it here.\n" +
                 "You can use the **{prefix}setprefix [prefix]** command to set a prefix if you're an Administrator.")]
        public async Task GetServerPrefix()
        {
            string reply;
            if (Context.Guild == null)
            {
                reply = "You can use commands without any prefix in a DM with me!";
            }
            else
            {
                string prefix = storage.GetPrefix(Context.Guild.Id);
                reply = $"Prefix for this server is set to '{prefix}'{" (the default)".If(prefix == storage.DefaultPrefix)}. It can be changed with the command **setprefix**.";
            }
            await ReplyAsync(reply);
        }


        [Command("feedback"), Alias("suggestion", "bug"), Remarks("Send a message to the bot's developer")]
        [Summary("Whatever text you write after this command will be sent directly to the bot's developer. "
               + "You may receive an answer through the bot in a DM.")]
        public async Task SendFeedback([Remainder]string message)
        {
            try
            {
                File.AppendAllText(BotFile.FeedbackLog, $"[{Context.User.FullName()}] {message}\n\n");
                await ReplyAsync($"{CustomEmoji.Check} Message sent. Thank you!");
                await (await Context.Client.GetApplicationInfoAsync()).Owner.SendMessageAsync($"```diff\n+Feedback received: {Context.User.FullName()}```\n{message}");
            }
            catch (Exception e)
            {
                await logger.Log(LogSeverity.Error, $"{e}");
                await ReplyAsync("Oops, I didn't catch that. Please try again.");
            }
        }


        [Command("invite"), Alias("inv"), Remarks("Invite this bot to your server")]
        [Summary("Shows a fancy embed block with the bot's invite link. I'd show it right now too, since you're already here, but I really want you to see that fancy embed.")]
        [RequireBotPermissionImproved(ChannelPermission.EmbedLinks)]
        public async Task SendBotInvite()
        {
            var embed = new EmbedBuilder()
            {
                Title = "Bot invite link",
                Color = new Color(241, 195, 15),
                ThumbnailUrl = Context.Client.CurrentUser.GetAvatarUrl(ImageFormat.Auto, 128)
            };
            embed.AddField($"➡ <{storage.BotContent["shortinvite"]}>", "*Thanks for inviting Pac-Man Bot!*", false);
            await ReplyAsync("", false, embed.Build());
        }




        [Command("party"), Alias("blob", "dance"), HideHelp]
        [Summary("Takes a number which can be either an amount of emotes to send or a message ID to react to. Reacts to the command by default.")]
        public async Task BlobDanceFast(ulong number = 0)
        {
            if (number < 1) await Context.Message.AddReactionAsync(CustomEmoji.Dance);
            else if (number <= 10) await ReplyAsync($"{CustomEmoji.Dance}".Multiply((int)number));
            else if (number <= 1000000) await ReplyAsync($"Are you insane?");
            else // Message ID
            {
                if (await Context.Channel.GetMessageAsync(number) is IUserMessage message) await message.AddReactionAsync(CustomEmoji.Dance);
                else await Context.Message.AddReactionAsync(CustomEmoji.Cross);
            }
        }


        [Command("spamparty"), Alias("spamblob", "spamdance"), HideHelp]
        [Summary("Reacts to everything with a blob dance emote. Only usable by a moderator.")]
        [RequireUserPermissionImproved(ChannelPermission.ManageMessages), RequireBotPermissionImproved(ChannelPermission.AddReactions)]
        public async Task SpamDance(int amount = 5)
        {
            foreach (IUserMessage message in Context.Channel.GetCachedMessages(amount))
            {
                await message.AddReactionAsync(CustomEmoji.Dance);
            }
        }


        [Command("command"), ExampleUsage("help play"), HideHelp]
        [Summary("This is not a real command. If you want to see help for a specific command, please do **{prefix}help [command name]**, where \"[command name]\" is the name of a command.")]
        public async Task DoNothing() => await logger.Log(LogSeverity.Verbose, "Someone tried to do \"<help command\"");
    }
}
