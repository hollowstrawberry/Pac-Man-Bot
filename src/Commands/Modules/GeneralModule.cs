using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using PacManBot.Constants;
using PacManBot.Extensions;
using PacManBot.Games;

namespace PacManBot.Commands.Modules
{
    [Module(ModuleNames.General)]
    [RequireBotPermissions(BaseBotPermissions)]
    public class GeneralModule : BasePmBotModule
    {
        private static readonly IEnumerable<string> GameNames = ReflectionExtensions.AllTypes
            .MakeObjects<BaseGame>()
            .OrderBy(g => g.GameIndex)
            .Select(g => g.GameName)
            .ToArray();


        [Command("about"), Aliases("info")]
        [Description("Shows relevant information, data and links about Pac-Man Bot.")]
        public async Task SendBotInfo(CommandContext ctx)
        {
            var app = await ctx.Client.GetCurrentApplicationAsync();
            var dsp = typeof(DiscordClient).Assembly.GetName().Version;

            int guilds = 0;
            foreach (var shard in ShardedClient.ShardClients.Values)
            {
                guilds += shard.Guilds.Count;
            }

            var embed = new DiscordEmbedBuilder()
                .WithTitle($"PacMan Bot {CustomEmoji.PacMan}•••")
                .WithDescription(Content.about.Replace("{prefix}", ctx.Prefix))
                .WithColor(Colors.PacManYellow)
                .AddField("Total guilds", $"{guilds}", true)
                .AddField("Total games", $"{Games.AllGames.Count()}", true)
                .AddField("Host", Environment.MachineName, true)
                .AddField("Owner", app.Owners.Select(x => x.NameandDisc()).JoinString(", "), true)
                .AddField("Bot version", Program.Version, true)
                .AddField("Library", $"DSharpPlus {dsp.Major}.{dsp.Minor}.{dsp.Revision}", true);

            foreach (var (name, desc) in Content.aboutFields)
            {
                embed.AddField(name, desc, true);
            }

            await ctx.RespondAsync(embed);
        }


        [Command("status")]
        [Description("Current process information about the bot.")]
        public async Task SendBotStatus(CommandContext ctx)
        {
            var process = Process.GetCurrentProcess();

            int guilds = 0;
            foreach (var shard in ShardedClient.ShardClients.Values)
            {
                guilds += shard.Guilds.Count;
            }

            var embed = new DiscordEmbedBuilder()
                .WithTitle($"PacMan Bot {CustomEmoji.PacMan}•••")
                .WithColor(Colors.PacManYellow)

                .AddField("Latency", $"{ctx.Client.Ping}ms", true)
                .AddField("Total guilds", $"{guilds}", true)
                .AddField("Total games", $"{Games.AllGames.Count()}", true)

                .AddField("Memory", $"{process.PrivateMemorySize64 / 1024 / 1024.0:n2} MB", true)
                .AddField("Threads", $"{process.Threads.Count}", true)
                .AddField("Shards", $"{ctx.Client.ShardCount}", true)

                .AddField("Uptime", (DateTime.Now - process.StartTime).Humanized(3), false);

            await ctx.RespondAsync(embed);
        }


        [Command("waka"), Aliases("ping")]
        [Description("Check how quickly the bot is responding to commands.")]
        public async Task Ping(CommandContext ctx, [RemainingText]string waka = "")
        {
            var stopwatch = Stopwatch.StartNew(); // Measure the time it takes to send a message to chat
            var message = await ctx.RespondAsync($"{CustomEmoji.Loading} Waka");
            stopwatch.Stop();

            var content = $"{CustomEmoji.PacMan} Waka in `{(int)stopwatch.ElapsedMilliseconds}`ms **|** Latency `{ctx.Client.Ping}`ms";

            if (ctx.Client.ShardCount > 1)
            {
                content += $" **|** `Shard {ctx.Client.ShardId + 1}/{ctx.Client.ShardCount}`";
            }

            await message.ModifyAsync(content);                   
        }


        [Command("allgames"), Aliases("gamestats")]
        [Description("Shows information about all active games managed by the bot.")]
        public async Task GameStats(CommandContext ctx)
        {
            var embed = new DiscordEmbedBuilder
            {
                Color = Colors.PacManYellow,
                Title = $"Active games in all guilds {CustomEmoji.PacMan}•••",
            };

            foreach (var name in GameNames)
            {
                embed.AddField(name, Games.AllGames.Where(g => g.GameName == name).Count().ToString(), true);
            }

            await ctx.RespondAsync(embed);
        }


        [Command("prefix"), Hidden]
        [Description("Shows this bot's prefix for this server, even though you can already see it here.\n" +
                 "You can use the `setprefix` command to set a prefix if you're an Administrator.")]
        public async Task GetServerPrefix(CommandContext ctx)
        {
            string message;
            if (ctx.Guild == null)
            {
                message = "You can use commands without any prefix in a DM with me!";
            }
            else
            {
                message = $"Prefix for this server is set to `{Storage.GetGuildPrefix(ctx.Guild)}`" +
                          " (the default)".If(Storage.GetGuildPrefix(ctx.Guild) == Storage.DefaultPrefix) +
                          $". It can be changed using the command `{ctx.Prefix}setprefix`";

                if (ctx.Prefix == "")
                {
                    message += "\n\nThis channel is in **No Prefix mode**, and using the prefix is unnecessary.\n" +
                               "Use `help toggleprefix` for more info.";
                }
            }

            await ctx.RespondAsync(message);
        }


        [Command("invite"), Aliases("inv")]
        [Description("Shows a fancy embed block with the bot's invite link. " +
                 "I'd show it right now too, since you're already here, but I really want you to see that fancy embed.")]
        public async Task SendBotInvite(CommandContext ctx)
        {
            var embed = new DiscordEmbedBuilder()
                .WithTitle("Bot invite link")
                .WithColor(Colors.PacManYellow)
                .WithThumbnail(ctx.Client.CurrentUser.GetAvatarUrl(ImageFormat.Auto))
                .WithUrl(Content.inviteLink)
                .AddField($"➡ <{Content.inviteLink}>", "Thanks for inviting Pac-Man Bot!");

            await ctx.RespondAsync(embed);
        }


        [Command("server"), Aliases("support")]
        [Description(CustomEmoji.Staff + " Link to the Pac-Man discord server")]
        public async Task SendBotServer(CommandContext ctx)
        {
            var embed = new DiscordEmbedBuilder()
                .WithTitle("Pac-Man Bot Support server")
                .WithUrl(Content.serverLink)
                .WithDescription($"{CustomEmoji.Staff} We'll be happy to see you there!")
                .WithColor(Colors.PacManYellow)
                .WithThumbnail("https://cdn.discordapp.com/icons/409803292219277313/d41cc5c5674ff9be45615b73738b85e2.jpg");

            await ctx.RespondAsync(embed);
        }


        [Command("github"), Aliases("git")]
        [Description(CustomEmoji.GitHub + "Link to Pac-Man's GitHub repository. I welcome contributions!")]
        public async Task SendBotGitHub(CommandContext ctx)
        {
            var embed = new DiscordEmbedBuilder()
                .WithTitle("Pac-Man Bot GitHub repository")
                .WithUrl(Content.githubLink)
                .WithDescription($"{CustomEmoji.GitHub} Contributions welcome!")
                .WithColor(Colors.PacManYellow)
                .WithThumbnail("https://cdn.discordapp.com/attachments/541768631445618689/541768699929952257/GitHub.png");

            await ctx.RespondAsync(embed);
        }



        [Command("donate"), Aliases("donation", "donations", "paypal")]
        [Description("Show donation info for this bot's developer.")]
        public async Task SendDonationInfo(CommandContext ctx)
        {
            var embed = new DiscordEmbedBuilder()
                .WithTitle("Donations")
                .WithUrl("http://paypal.me/samrux")
                .WithColor(Colors.PacManYellow)
                .WithThumbnail("https://upload.wikimedia.org/wikipedia/commons/a/a4/Paypal_2014_logo.png")
                .WithDescription($"You can donate to OrchidAlloy, the creator of this bot.\n" +
                    $"Donations support development and pay the hosting costs of the bot.\n" +
                    $"[Click here to go to my PayPal](http://paypal.me/samrux)");

            await ctx.RespondAsync(embed);
        }


        [Command("feedback"), Aliases("suggestion", "bugreport")]
        [Description("Whatever text you write after this command will be sent directly to the bot's developer. " +
                 "You may receive an answer through the bot in a DM.")]
        public async Task SendFeedback(CommandContext ctx, [RemainingText]string message)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(message))
                {
                    await ctx.RespondAsync("You may use this command to send a message to the bot's developer.");
                    return;
                }

                File.AppendAllText(Files.FeedbackLog, $"[{ctx.User.DebugName()}] {message}\n\n");

                // this shouldn't be this complicated
                var app = await ctx.Client.GetCurrentApplicationAsync();
                foreach (var owner in app.Owners)
                    foreach (var shard in ShardedClient.ShardClients.Values)
                        foreach (var guild in shard.Guilds.Values)
                            if (guild.Members.TryGetValue(owner.Id, out var ownerMember))
                            {
                                string content = $"```diff\n+Feedback received: {ctx.User.DebugName()}```\n{message}".Truncate(2000);
                                await ownerMember.SendMessageAsync(content);
                                await ctx.RespondAsync($"{CustomEmoji.Check} Message sent. Thank you!");
                                return;
                            }
                throw new InvalidOperationException("Couldn't find owner member");
            }
            catch (Exception e)
            {
                Log.Exception($"Sending feedback from {ctx.User.DebugName()} at {ctx.Channel.DebugName()}", e);
                await ctx.RespondAsync("Oops, I didn't catch that, please try again. " +
                    "If this keeps happening join the support server to let my owner know.");
            }
        }
    }
}
