using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using PacManBot.Games;
using PacManBot.Constants;
using PacManBot.Extensions;

namespace PacManBot.Commands.Modules
{
    [Name("📁General"), Remarks("1")]
    public class GeneralModule : BaseCustomModule
    {
        private static readonly IEnumerable<string> GameNames = typeof(BaseGame).Assembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(BaseGame)) && t.IsClass && !t.IsAbstract)
            .Select(t => (BaseGame)Activator.CreateInstance(t, true))
            .OrderBy(t => t.GameIndex)
            .Select(t => t.GameName);



        public CommandService Commands { get; }

        public GeneralModule(IServiceProvider services) : base(services)
        {
            Commands = services.Get<CommandService>();
        }



        [Command("about"), Alias("info"), Remarks("About this bot")]
        [Summary("Shows relevant information, data and links about Pac-Man Bot.")]
        [BetterRequireBotPermission(ChannelPermission.EmbedLinks)]
        public async Task SendBotInfo()
        {
            var embed = new EmbedBuilder
            {
                Title = $"{CustomEmoji.PacMan} __**Pac-Man Bot**__",
                Description = Content.about.Replace("{prefix}", Prefix),
                Color = Colors.PacManYellow,
            };
            embed.AddField("Total guilds", $"{Context.Client.Guilds.Count}", true);
            embed.AddField("Total games", $"{Games.AllGames.Count()}", true);
            embed.AddField("Latency", $"{Context.Client.Latency}ms", true);

            foreach (var (name, desc) in Content.aboutFields)
            {
                embed.AddField(name, desc, true);
            }

            await ReplyAsync(embed);
        }


        [Command("help"), Alias("h", "commands"), Parameters("[command]"), Remarks("List of commands or help about a command")]
        [Summary("Show a complete list of commands you can use. You can specify a command to see detailed help about that command.")]
        [BetterRequireBotPermission(ChannelPermission.EmbedLinks)]
        public async Task SendCommandHelp([Remainder]string commandName)
        {
            CommandInfo command = Commands.Commands.FirstOrDefault(c => c.Aliases.Contains(commandName));
            if (command == null)
            {
                await ReplyAsync($"Can't find a command with that name. Use `{Prefix}help` for a list of commands.");
                return;
            }

            var helpInfo = new CommandHelpInfo(command);


            var embed = new EmbedBuilder
            {
                Title = $"__Command__: {Prefix}{command.Name}",
                Color = Colors.PacManYellow
            };

            if (helpInfo.Hidden) embed.AddField("Hidden command", "*Are you a wizard?*", true);

            if (helpInfo.Parameters != "") embed.AddField("Parameters", helpInfo.Parameters, true);

            if (command.Aliases.Count > 1)
            {
                string aliasList = "";
                for (int i = 1; i < command.Aliases.Count; i++) aliasList += $"{", ".If(i > 1)}{Prefix}{command.Aliases[i]}";
                embed.AddField("Aliases", aliasList, true);
            }

            if (helpInfo.Summary != "")
            {
                foreach (string section in helpInfo.Summary
                    .Replace("{prefix}", Prefix)
                    .Split("\n\n\n"))
                {
                    embed.AddField("Summary", section + "ᅠ");
                }
            }

            if (helpInfo.ExampleUsage != "") embed.AddField("Example Usage", helpInfo.ExampleUsage.Replace("{prefix}", Prefix));

            await ReplyAsync(embed);
        }



        [Command("help"), Alias("h", "commands"), HideHelp]
        [BetterRequireBotPermission(ChannelPermission.EmbedLinks)]
        public async Task SendAllHelpNoRemarks() => await SendAllHelp(false);

        [Command("helpfull"), Alias("helpmore", "hf", "commandsfull"), Remarks("Expanded help about all commands")]
        [Summary("Show a complete list of all commands including their parameters and a short description.")]
        [BetterRequireBotPermission(ChannelPermission.EmbedLinks)]
        public async Task SendAllHelpWithRemarks() => await SendAllHelp(true);


        private async Task SendAllHelp(bool expanded)
        {
            var embed = new EmbedBuilder()
            {
                Title = $"{CustomEmoji.PacMan} __**Bot Commands**__",
                Description = (Prefix == "" ? "No prefix is needed in this channel!" : $"Prefix for this server is '{Prefix}'")
                            + $"\nYou can do **{Prefix}help command** for more information about a command.\n\n"
                            + $"Parameters: [optional] <needed>".If(expanded),
                Color = Colors.PacManYellow
            };

            foreach (var module in Commands.Modules.OrderBy(m => m.Remarks))
            {
                var moduleText = new StringBuilder();

                foreach (var command in module.Commands.OrderBy(c => c.Priority))
                {
                    var helpInfo = new CommandHelpInfo(command);

                    if (!helpInfo.Hidden)
                    {
                        var conditions = await command.CheckPreconditionsAsync(Context);
                        if (!conditions.IsSuccess) continue;

                        if (expanded)
                        {
                            moduleText.Append($"**{command.Name} {helpInfo.Parameters}**");
                            if (helpInfo.Remarks != "") moduleText.Append($" — *{helpInfo.Remarks}*");
                            moduleText.Append("\n");
                        }
                        else
                        {
                            moduleText.Append($"**{command.Name}**, ");
                        }
                    }
                }

                if (!expanded && module.Name.Contains("Pac-Man")) moduleText.Append("**bump**, **cancel**");

                if (moduleText.Length > 0)
                {
                    embed.AddField(module.Name, moduleText.ToString().Trim(' ', ',', '\n'));
                }
            }

            await ReplyAsync(embed);
        }



        [Command("waka"), Alias("ping"), Parameters(""), Remarks("Like ping, but waka")]
        [Summary("Check how quickly the bot is responding to commands.")]
        public async Task Ping([Remainder]string uselessArgs = "")
        {
            var stopwatch = Stopwatch.StartNew(); // Measure the time it takes to send a message to chat
            var message = await ReplyAsync($"{CustomEmoji.Loading} Waka");
            stopwatch.Stop();

            var content = new StringBuilder();
            content.Append($"{CustomEmoji.PacMan} Waka in `{(int)stopwatch.ElapsedMilliseconds}`ms");

            if (Context.Client.Shards.Count > 1)
            {
                var shard = Context.Client.GetShardFor(Context.Guild);
                content.Append($" **|** `Shard {shard.ShardId + 1}/{Context.Client.Shards.Count}`");
            }

            await message.ModifyAsync(m => m.Content = content.ToString(), DefaultOptions);                   
        }


        [Command("games"), Alias("gamestats"), Parameters(""), Remarks("Info about the bot's current games")]
        [Summary("Shows information about all active games managed by the bot.")]
        public async Task GameStats([Remainder]string uselessArgs = "")
        {
            var embed = new EmbedBuilder
            {
                Color = Colors.PacManYellow,
                Title = $"{CustomEmoji.PacMan} Active games in all guilds",
                Fields = GameNames.Select(name => new EmbedFieldBuilder {
                    Name = name,
                    Value = Games.AllGames.Where(g => g.GameName == name).Count(),
                    IsInline = true
                }).ToList()
            };

            await ReplyAsync(embed);
        }


        [Command("prefix"), HideHelp]
        [Summary("Shows this bot's prefix for this server, even though you can already see it here.\n" +
                 "You can use the **{prefix}setprefix** command to set a prefix if you're an Administrator.")]
        public async Task GetServerPrefix()
        {
            string message;
            if (Context.Guild == null)
            {
                message = "You can use commands without any prefix in a DM with me!";
            }
            else
            {
                message = $"Prefix for this server is set to `{AbsolutePrefix}`" +
                          " (the default)".If(AbsolutePrefix == Storage.DefaultPrefix) +
                          $". It can be changed using the command `{Prefix}setprefix`";

                if (Prefix == "")
                {
                    message += "\n\nThis channel is in **No Prefix mode**, and using the prefix is unnecessary.\n" +
                               "Use `help toggleprefix` for more info.";
                }
            }

            await ReplyAsync(message);
        }


        [Command("invite"), Alias("inv"), Remarks("Invite this bot to your server")]
        [Summary("Shows a fancy embed block with the bot's invite link. " +
                 "I'd show it right now too, since you're already here, but I really want you to see that fancy embed.")]
        [BetterRequireBotPermission(ChannelPermission.EmbedLinks)]
        public async Task SendBotInvite()
        {
            var embed = new EmbedBuilder()
            {
                Title = "Bot invite link",
                Color = Colors.PacManYellow,
                ThumbnailUrl = Context.Client.CurrentUser.GetAvatarUrl(),
                Url = Content.invite,
                Fields = new List<EmbedFieldBuilder>
                {
                    new EmbedFieldBuilder
                    {
                        Name = $"➡ <{Content.invite}>",
                        Value = "Thanks for inviting Pac-Man Bot!",
                    },
                },
            };

            await ReplyAsync(embed);
        }


        [Command("server"), Alias("support"), Remarks("Support server link")]
        [Summary(CustomEmoji.Staff + " Link to the Pac-Man discord server")]
        [BetterRequireBotPermission(ChannelPermission.EmbedLinks)]
        public async Task SendBotServer()
        {
            var embed = new EmbedBuilder()
            {
                Title = "Pac-Man Bot Support server",
                Url = "https://discord.gg/hGHnfda",
                Description = $"{CustomEmoji.Staff} We'll be happy to see you there!",
                Color = Colors.PacManYellow,
                ThumbnailUrl = Context.Client.GetGuild(409803292219277313).IconUrl,
            };

            await ReplyAsync(embed);
        }


        [Command("github"), Alias("git"), Remarks("GitHub repository link")]
        [Summary(CustomEmoji.GitHub + "Link to Pac-Man's GitHub repository. I welcome contributions!")]
        [BetterRequireBotPermission(ChannelPermission.EmbedLinks)]
        public async Task SendBotGitHub()
        {
            var embed = new EmbedBuilder()
            {
                Title = "Pac-Man Bot GitHub repository",
                Url = "https://github.com/Samrux/Pac-Man-Bot",
                Description = $"{CustomEmoji.GitHub} Contributions welcome!",
                Color = Colors.PacManYellow,
                ThumbnailUrl = "https://cdn.discordapp.com/attachments/412090039686660097/455914771179503633/GitHub.png",
            };

            await ReplyAsync(embed);
        }


        [Command("feedback"), Alias("suggestion", "bug"), Remarks("Send a message to the bot's developer")]
        [Summary("Whatever text you write after this command will be sent directly to the bot's developer. " +
                 "You may receive an answer through the bot in a DM.")]
        public async Task SendFeedback([Remainder]string message)
        {
            try
            {
                File.AppendAllText(Files.FeedbackLog, $"[{Context.User.FullName()}] {message}\n\n");
                await ReplyAsync($"{CustomEmoji.Check} Message sent. Thank you!");
                string content = $"```diff\n+Feedback received: {Context.User.FullName()}```\n{message}".Truncate(2000);
                await Storage.AppInfo.Owner.SendMessageAsync(content, options: DefaultOptions);
            }
            catch (Exception e)
            {
                await Logger.Log(LogSeverity.Error, $"{e}");
                await ReplyAsync("Oops, I didn't catch that, please try again. Maybe the developer screwed up");
            }
        }




        [Command("party"), Alias("blob", "dance"), HideHelp]
        [Summary("Takes a number which can be either an amount of emotes to send or a message ID to react to. " +
                 "Reacts to the command by default.")]
        public async Task BlobDance(ulong number = 0)
        {
            if (number < 1) await Context.Message.AddReactionAsync(CustomEmoji.ERapidBlobDance, DefaultOptions);
            else if (number <= 10) await ReplyAsync(CustomEmoji.RapidBlobDance.Repeat((int)number));
            else if (number <= 1000000) await ReplyAsync("Are you insane?");
            else // Message ID
            {
                if (await Context.Channel.GetMessageAsync(number) is IUserMessage message)
                {
                    await message.AddReactionAsync(CustomEmoji.ERapidBlobDance, DefaultOptions);
                }
                else await AutoReactAsync(false);
            }
        }


        [Command("spamparty"), Alias("spamblob", "spamdance"), HideHelp]
        [Summary("Reacts to everything with a blob dance emote. Only usable by a moderator.")]
        [BetterRequireUserPermission(ChannelPermission.ManageMessages)]
        [BetterRequireBotPermission(ChannelPermission.AddReactions)]
        public async Task SpamDance(int amount = 5)
        {
            var messages = await Context.Channel.GetMessagesAsync(Math.Min(amount, 10)).FlattenAsync();
            foreach (var message in messages.OfType<SocketUserMessage>())
            {
                await message.AddReactionAsync(CustomEmoji.ERapidBlobDance, DefaultOptions);
            }
        }


        [Command("neat"), Summary("Neat"), HideHelp]
        public async Task Neat() => await ReplyAsync("neat");

        [Command("nice"), Summary("Nice"), HideHelp]
        public async Task Nice() => await ReplyAsync("nice");



        [Command("command"), ExampleUsage("help play"), HideHelp]
        [Summary("This is not a real command. If you want to see help for a specific command, please do `{prefix}help [command name]`, " +
                 "where \"[command name]\" is the name of a command.")]
        public async Task DoNothing() => await Logger.Log(LogSeverity.Info, "Someone tried to do \"<command\"");
    }
}
