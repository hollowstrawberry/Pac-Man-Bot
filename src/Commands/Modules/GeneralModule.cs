using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using PacManBot.Constants;
using PacManBot.Extensions;
using PacManBot.Games;
using PacManBot.Services;

namespace PacManBot.Commands.Modules
{
    [Name(ModuleNames.General), Remarks("1")]
    public class GeneralModule : BaseModule
    {
        private static readonly IEnumerable<string> GameNames = ReflectionExtensions.AllTypes
            .MakeObjects<BaseGame>()
            .OrderBy(g => g.GameIndex)
            .Select(g => g.GameName)
            .ToArray();


        public PmCommandService Commands { get; set; }
        public GameService Games { get; set; }


        [Command("about"), Alias("info"), Remarks("About this bot")]
        [Summary("Shows relevant information, data and links about Pac-Man Bot.")]
        public async Task SendBotInfo()
        {
            var app = await Context.Client.GetApplicationInfoAsync();
            var dnetv = typeof(IDiscordClient).Assembly.GetName().Version;

            var embed = new EmbedBuilder
            {
                Title = $"{CustomEmoji.PacMan} __**Pac-Man Bot**__",
                Description = Content.about.Replace("{prefix}", Context.Prefix),
                Color = Colors.PacManYellow,
            };
            embed.AddField("Total guilds", $"{Context.Client.Guilds.Count}", true);
            embed.AddField("Total games", $"{Games.AllGames.Count()}", true);
            embed.AddField("Host", Environment.MachineName, true);
            embed.AddField("Owner", app.Owner.NameandDisc(), true);
            embed.AddField("Bot version", Program.Version, true);
            embed.AddField("Library", $"Discord.Net {dnetv.Major}.{dnetv.Minor} (C#)", true);

            foreach (var (name, desc) in Content.aboutFields)
            {
                embed.AddField(name, desc, true);
            }

            await ReplyAsync(embed);
        }


        [Command("status"), Remarks("Bot status")]
        [Summary("Current process information about the bot.")]
        public async Task SendBotStatus()
        {
            var process = Process.GetCurrentProcess();

            var embed = new EmbedBuilder
            {
                Title = $"{CustomEmoji.PacMan} __**Pac-Man Bot**__",
                Color = Colors.PacManYellow,
            };

            embed.AddField("Latency", $"{Context.Client.Latency}ms", true);
            embed.AddField("Total guilds", $"{Context.Client.Guilds.Count}", true);
            embed.AddField("Total games", $"{Games.AllGames.Count()}", true);

            embed.AddField("Memory", $"{process.PrivateMemorySize64 / 1024 / 1024.0:n2} MB", true);
            embed.AddField("Threads", process.Threads.Count, true);
            embed.AddField("Shards", $"{Context.Client.Shards.Count()}", true);

            embed.AddField("Uptime", (DateTime.Now - process.StartTime).Humanized(3), false);

            await ReplyAsync(embed);
        }



        [Command("help"), Alias("commands", "games", "play"), Parameters("[command]")]
        [Remarks("Help about commands or a specific command")]
        [Summary("Show a complete list of commands you can use. You can specify a command to see detailed help about that command.")]
        public async Task SendHelp([Remainder]string command = null)
        {
            if (command == null)
            {
                await ReplyAsync(await Commands.GetAllHelp(Context, expanded: false));
            }
            else
            {
                var embed = Commands.GetCommandHelp(command, Context);
                string message = embed == null ? $"Can't find a command with that name. Use `{Context.Prefix}help` for a list of commands." : "";
                await ReplyAsync(message, embed);
            }
        }


        [Command("helpfull"), Alias("helpmore", "hf", "commandsfull"), Remarks("Expanded help about all commands")]
        [Summary("Show a complete list of all commands including their parameters and a short description.")]
        public async Task SendAllHelpExpanded()
        {
            await ReplyAsync(await Commands.GetAllHelp(Context, expanded: true));
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


        [Command("say"), Remarks("Make the bot say anything"), Priority(-1)]
        [Summary("Repeats back any message.")]
        [RequireContext(ContextType.DM)]
        public async Task Say([Remainder]string message)
            => await ReplyAsync(message.SanitizeMentions());


        [Command("allgames"), Alias("gamestats"), Parameters(""), Remarks("Info about the bot's current games")]
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
                message = $"Prefix for this server is set to `{Context.FixedPrefix}`" +
                          " (the default)".If(Context.FixedPrefix == Storage.DefaultPrefix) +
                          $". It can be changed using the command `{Context.Prefix}setprefix`";

                if (Context.Prefix == "")
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
        public async Task SendBotInvite()
        {
            var embed = new EmbedBuilder()
            {
                Title = "Bot invite link",
                Color = Colors.PacManYellow,
                ThumbnailUrl = Context.Client.CurrentUser.GetAvatarUrl(),
                Url = Content.inviteLink,
                Fields = new List<EmbedFieldBuilder>
                {
                    new EmbedFieldBuilder
                    {
                        Name = $"➡ <{Content.inviteLink}>",
                        Value = "Thanks for inviting Pac-Man Bot!",
                    },
                },
            };

            await ReplyAsync(embed);
        }


        [Command("server"), Alias("support"), Remarks("Support server link")]
        [Summary(CustomEmoji.Staff + " Link to the Pac-Man discord server")]
        public async Task SendBotServer()
        {
            var embed = new EmbedBuilder()
            {
                Title = "Pac-Man Bot Support server",
                Url = Content.serverLink,
                Description = $"{CustomEmoji.Staff} We'll be happy to see you there!",
                Color = Colors.PacManYellow,
                ThumbnailUrl = Context.Client.GetGuild(409803292219277313).IconUrl,
            };

            await ReplyAsync(embed);
        }


        [Command("github"), Alias("git"), Remarks("GitHub repository link")]
        [Summary(CustomEmoji.GitHub + "Link to Pac-Man's GitHub repository. I welcome contributions!")]
        public async Task SendBotGitHub()
        {
            var embed = new EmbedBuilder()
            {
                Title = "Pac-Man Bot GitHub repository",
                Url = Content.githubLink,
                Description = $"{CustomEmoji.GitHub} Contributions welcome!",
                Color = Colors.PacManYellow,
                ThumbnailUrl = "https://cdn.discordapp.com/attachments/541768631445618689/541768699929952257/GitHub.png",
            };

            await ReplyAsync(embed);
        }



        [Command("donate"), Alias("donation", "donations", "paypal"), Remarks("Donate to the bot's the developer")]
        [Summary("Show donation info for this bot's developer.")]
        public async Task SendDonationInfo()
        {
            var embed = new EmbedBuilder()
            {
                Title = "Donations",
                Url = "http://paypal.me/samrux",
                Color = Colors.PacManYellow,
                ThumbnailUrl = "https://upload.wikimedia.org/wikipedia/commons/a/a4/Paypal_2014_logo.png",

                Description =
                $"You can donate to OrchidAlloy, the creator of this bot.\n" +
                $"Donations support development and pay the hosting costs of the bot.\n" +
                $"[Click here to go to my PayPal](http://paypal.me/samrux)"
            };

            await ReplyAsync(embed);
        }


        [Command("feedback"), Alias("suggestion", "bugreport"), Remarks("Send a message to the bot's developer")]
        [Summary("Whatever text you write after this command will be sent directly to the bot's developer. " +
                 "You may receive an answer through the bot in a DM.")]
        public async Task SendFeedback([Remainder]string message)
        {
            try
            {
                File.AppendAllText(Files.FeedbackLog, $"[{Context.User.FullName()}] {message}\n\n");
                await ReplyAsync($"{CustomEmoji.Check} Message sent. Thank you!");
                string content = $"```diff\n+Feedback received: {Context.User.FullName()}```\n{message}".Truncate(2000);

                var app = await Context.Client.GetApplicationInfoAsync(PmBot.DefaultOptions);
                await app.Owner.SendMessageAsync(content, options: DefaultOptions);
            }
            catch (Exception e)
            {
                Log.Exception($"Sending feedback from {Context.User.FullName()} at {Context.Channel.FullName()}", e);
                await ReplyAsync("Oops, I didn't catch that, please try again. I think the developer messed up big time.");
            }
        }




        [Command("party"), Alias("blob", "dance"), HideHelp]
        [Summary("Takes a number which can be either an amount of emotes to send or a message ID to react to. " +
                 "Reacts to the command by default.")]
        [PmRequireBotPermission(ChannelPermission.UseExternalEmojis | ChannelPermission.AddReactions)]
        public async Task BlobDance(ulong number = 0)
        {
            if (number < 1) await Context.Message.AddReactionAsync(CustomEmoji.EBlobDance, DefaultOptions);
            else if (number <= 10) await ReplyAsync(CustomEmoji.BlobDance.Repeat((int)number));
            else if (number <= 1000000) await ReplyAsync("Are you insane?");
            else // Message ID
            {
                if (await Context.Channel.GetMessageAsync(number) is IUserMessage message)
                {
                    await message.AddReactionAsync(CustomEmoji.EBlobDance, DefaultOptions);
                }
                else await AutoReactAsync(false);
            }
        }


        [Command("spamparty"), Alias("spamblob", "spamdance"), HideHelp]
        [Summary("Reacts to everything with a blob dance emote. Only usable by a moderator.")]
        [PmRequireUserPermission(ChannelPermission.ManageMessages)]
        [PmRequireBotPermission(ChannelPermission.AddReactions)]
        public async Task SpamDance(int amount = 5)
        {
            var messages = await Context.Channel.GetMessagesAsync(Math.Min(amount, 10)).FlattenAsync();
            foreach (var message in messages.OfType<SocketUserMessage>())
            {
                await message.AddReactionAsync(CustomEmoji.EBlobDance, DefaultOptions);
            }
        }


        [Command("neat"), Summary("Neat"), HideHelp]
        public async Task Neat() => await ReplyAsync("neat");

        [Command("nice"), Summary("Nice"), HideHelp]
        public async Task Nice() => await ReplyAsync("nice");



        [Command("command"), ExampleUsage("help play"), HideHelp]
        [Summary("This is not a real command. If you want to see help for a specific command, please do `{prefix}help [command name]`, " +
                 "where \"[command name]\" is the name of a command.")]
        public void DoNothing() => Log.Debug("Someone tried to do \"<command\" lol");
    }
}
