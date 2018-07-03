using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using PacManBot.Constants;
using PacManBot.Extensions;

namespace PacManBot.Commands
{
    [Name("📁General"), Remarks("1")]
    public class GeneralModule : BaseCustomModule
    {
        public CommandService Commands { get; }

        public GeneralModule(IServiceProvider services) : base(services)
        {
            Commands = services.Get<CommandService>();
        }



        [Command("about"), Alias("a", "info"), Remarks("About this bot")]
        [Summary("Shows relevant information, data and links about Pac-Man Bot.")]
        [BetterRequireBotPermission(ChannelPermission.EmbedLinks)]
        public async Task SendBotInfo()
        {
            string description = Storage.BotContent["about"].Replace("{prefix}", Prefix);
            var fields = Storage.BotContent["aboutfields"].Split('\n').Where(s => s.Contains("|")).ToArray();

            var embed = new EmbedBuilder
            {
                Title = $"{CustomEmoji.PacMan} __**Pac-Man Bot**__",
                Description = description,
                Color = Colors.PacManYellow,
            };
            embed.AddField("Total guilds", $"{Context.Client.Guilds.Count}", true);
            embed.AddField("Total active games", $"{Storage.Games.Count}", true);
            embed.AddField("Latency", $"{Context.Client.Latency}ms", true);

            foreach (var field in fields.Select(x => x.Split('|')))
            {
                embed.AddField(field[0], field[1], true);
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
                foreach (string section in helpInfo.Summary.Replace("{prefix}", Prefix).Split("\n\n\n"))
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

        [Command("helpfull"), Alias("hf", "commandsfull")]
        [BetterRequireBotPermission(ChannelPermission.EmbedLinks)]
        public async Task SendAllHelpWithRemarks() => await SendAllHelp(true);


        public async Task SendAllHelp(bool showRemarks)
        {
            var embed = new EmbedBuilder()
            {
                Title = $"{CustomEmoji.PacMan} __**Bot Commands**__",
                Description = (Context.Guild == null ? "No prefix is needed in a DM!" : $"Prefix for this server is '{Prefix}'")
                            + $"\nYou can do **{Prefix}help command** for more information about a command.\n\nParameters: [optional] <needed>",
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

                        moduleText.Append($"**{command.Name} {helpInfo.Parameters}**");
                        if (showRemarks && helpInfo.Remarks != "") moduleText.Append($" — *{helpInfo.Remarks}*");
                        moduleText.Append('\n');
                    }
                }

                if (!showRemarks && module.Name.Contains("Pac-Man")) moduleText.Append("**bump**\n**cancel**\n");

                if (moduleText.Length > 0) embed.AddField(module.Name, moduleText.ToString(), true);
            }

            await ReplyAsync(embed);
        }



        [Command("waka"), Alias("ping"), Parameters(""), Remarks("Ping? Nah, waka.")]
        [Summary("Tests the ping (server reaction time in milliseconds) and shows other quick stats about the bot at the current moment.\n" +
                 "Did you know the bot responds every time you say \"waka\" in chat? Shhh, it's a secret.")]
        public async Task Ping([Remainder]string uselessArgs = "")
        {
            var stopwatch = Stopwatch.StartNew(); // Measure the time it takes to send a message to chat
            var message = await ReplyAsync($"{CustomEmoji.Loading} Waka");
            stopwatch.Stop();

            string content = $"{CustomEmoji.PacMan} Waka in `{(int)stopwatch.ElapsedMilliseconds}`ms **|** " +
                             $"{Context.Client.Guilds.Count} total guilds, {Storage.Games.Count} total active games";

            if (Context.Client.Shards.Count > 1)
            {
                var shard = Context.Client.GetShardFor(Context.Guild);

                int shardGames = 0;
                foreach (var game in Storage.Games)
                {
                    if (game.Guild != null && shard.Guilds.Contains(game.Guild) || game.Guild == null && shard.ShardId == 0)
                    {
                        shardGames++;
                    }
                }

                content += $"```css\nShard {shard.ShardId + 1}/{Context.Client.Shards.Count} " +
                           $"controlling {shard.Guilds.Count} guilds and {shardGames} games```";
            }

            await message.ModifyAsync(m => m.Content = content, DefaultOptions);                   
        }


        [Command("prefix"), HideHelp]
        [Summary("Shows this bot's prefix for this server, even though you can already see it here.\n" +
                 "You can use the `{prefix}setprefix [prefix]` command to set a prefix if you're an Administrator.")]
        public async Task GetServerPrefix()
        {
            await ReplyAsync(Context.Guild == null ? "You can use commands without any prefix in a DM with me!"
                : $"Prefix for this server is set to `{Prefix}`{" (the default)".If(Prefix == Storage.DefaultPrefix)}. " +
                  $"It can be changed using the command `{Prefix}setprefix`");
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
                Url = Storage.BotContent["shortinvite"],
                Fields = new List<EmbedFieldBuilder>
                {
                    new EmbedFieldBuilder
                    {
                        Name = $"➡ <{Storage.BotContent["shortinvite"]}>",
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
            else if (number <= 10) await ReplyAsync(CustomEmoji.RapidBlobDance.Multiply((int)number));
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
            foreach (SocketUserMessage message in messages.Where(x => x is SocketUserMessage))
            {
                await message.AddReactionAsync(CustomEmoji.ERapidBlobDance, DefaultOptions);
            }
        }


        [Command("neat"), HideHelp, Summary("Neat")]
        public async Task Neat() => await ReplyAsync("neat");

        [Command("nice"), HideHelp, Summary("Neat")]
        public async Task Nice() => await ReplyAsync("nice");



        [Command("command"), ExampleUsage("help play"), HideHelp]
        [Summary("This is not a real command. If you want to see help for a specific command, please do `{prefix}help [command name]`, " +
                 "where \"[command name]\" is the name of a command.")]
        public async Task DoNothing() => await Logger.Log(LogSeverity.Info, "Someone tried to do \"<command\"");
    }
}
