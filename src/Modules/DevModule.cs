using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using PacManBot.Services;
using PacManBot.Constants;

namespace PacManBot.Modules
{
    [Name("Developer")]
    [RequireOwner]
    public class DevModule : ModuleBase<SocketCommandContext>
    {
        private readonly DiscordShardedClient shardedClient;
        private readonly LoggingService logger;
        private readonly StorageService storage;
        private readonly ScriptingService scripting;


        public DevModule(DiscordShardedClient shardedClient, CommandService commands, LoggingService logger, StorageService storage, ScriptingService scripting)
        {
            this.shardedClient = shardedClient;
            this.logger = logger;
            this.storage = storage;
            this.scripting = scripting;
        }



        [Command("run"), Alias("eval", "runasync", "evalasync"), HideHelp]
        [Summary("Run code, super dangerous do not try at home. Developer only.")]
        public async Task ScriptEval([Remainder]string code)
        {
            try
            {
                await Context.Message.AddReactionAsync(CustomEmoji.Loading);
                await scripting.Eval(code, new ShardedCommandContext(shardedClient, Context.Message));
                await Context.Message.AddReactionAsync(CustomEmoji.Check);
            }
            catch (Exception e)
            {
                await ReplyAsync($"```cs\n{e.Message}```");
                await logger.Log(LogSeverity.Debug, "Eval", $"{e}");
                await Context.Message.AddReactionAsync(CustomEmoji.Cross);
            }
            finally
            {
                await Context.Message.RemoveReactionAsync(CustomEmoji.Loading, Context.Client.CurrentUser);
            }
        }


        [Command("feedbackreply"), Alias("reply"), HideHelp]
        [Summary("This is how Samrux replies to feedback. Developer only.")]
        public async Task ReplyFeedback(ulong useriD, [Remainder]string message)
        {
            try
            {
                await shardedClient.GetUser(useriD).SendMessageAsync("```diff\n+The following message was sent in response to your recent feedback." +
                                                                  "\n-To reply to this message, use the 'feedback' command again.```\n" + message);
                await Context.Message.AddReactionAsync(CustomEmoji.Check);
            }
            catch (Exception e)
            {
                await logger.Log(LogSeverity.Debug, $"{e.Message}");
                await ReplyAsync($"```{e.Message}```");
                await Context.Message.AddReactionAsync(CustomEmoji.Cross);
            }
        }


        [Command("reloadcontent"), Alias("reload"), HideHelp]
        [Summary("Reloads the content.bot file. Developer only")]
        public async Task ReloadContent()
        {
            storage.LoadBotContent();
            await logger.Log(LogSeverity.Info, "Reloaded bot content");
            await Context.Message.AddReactionAsync(CustomEmoji.Check);
        }


        [Command("log"), HideHelp]
        [Summary("Stores an entry in the bot logs. Developer only")]
        public async Task LogSomething([Remainder]string message)
        {
            await logger.Log(LogSeverity.Info, LogSource.Owner, message);
            await Context.Message.AddReactionAsync(CustomEmoji.Check);
        }


        [Command("garbagecollect"), Alias("gc"), HideHelp]
        [Summary("Clears unused memory if possible. Developer only.")]
        public async Task DoGarbageCollect()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            await Context.Message.AddReactionAsync(CustomEmoji.Check);
        }


        [Command("file"), Alias("readfile"), HideHelp, Parameters("[start] [length] <file>")]
        [Summary("Sends the contents of a file in the bot's host location. Developer only.")]
        public async Task ReadFile(int start, int length, [Remainder]string filename)
        {
            try
            {
                await ReplyAsync($"```{"cs".If(filename.Contains(".cs"))}\n{File.ReadAllText(filename).Replace("```", "`​``").Substring(start).Truncate(length)}".Truncate(1997) + "```");
            }
            catch (Exception e)
            {
                await logger.Log(LogSeverity.Debug, $"{e.Message}");
                await ReplyAsync($"```{e.Message}```");
            }
        }
        [Command("file"), Alias("readfile"), HideHelp]
        public async Task ReadFile(int start, [Remainder]string file) => await ReadFile(start, 2000, file);
        [Command("file"), Alias("readfile"), HideHelp]
        public async Task ReadFile([Remainder]string file) => await ReadFile(0, 2000, file);


        [Command("getguilds"), HideHelp]
        [Summary("Gets a list of guilds and member counts where this bot is in. Developer only.")]
        public async Task GetGuildMembers()
        {
            await ReplyAsync(string.Join("\n", shardedClient.Guilds.OrderByDescending(g => g.MemberCount).Select(g => $"{g.Name}: {g.MemberCount}")).Truncate(2000));
        }
    }
}
