using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using PacManBot.Services;
using PacManBot.Constants;
using PacManBot.Modules.PacMan;
using System.IO;
using System.Linq;

namespace PacManBot.Modules
{
    [Name("Developer")]
    [RequireOwner]
    public class DevModule : ModuleBase<SocketCommandContext>
    {
        private readonly LoggingService logger;
        private readonly StorageService storage;
        private readonly ScriptingService scripting;

        public DevModule(CommandService commands, LoggingService logger, StorageService storage, ScriptingService scripting)
        {
            this.logger = logger;
            this.storage = storage;
            this.scripting = scripting;
        }

        [Command("run"), Alias("eval", "runasync", "evalasync"), Remarks("code —"), Summary("Run code, super dangerous do not try at home. Developer only.")]
        public async Task ScriptEval([Remainder]string code)
        {
            try
            {
                await Context.Message.AddReactionAsync(CustomEmojis.Loading.ToEmote());
                await scripting.Eval(code, Context);
                await Context.Message.AddReactionAsync(CustomEmojis.Check.ToEmote());
            }
            catch (Exception exception)
            {
                await ReplyAsync($"```cs\n{exception.Message}```");
                await logger.Log(LogSeverity.Warning, $"{exception}");
                await Context.Message.AddReactionAsync(CustomEmojis.Cross.ToEmote());
            }
            finally
            {
                await Context.Message.RemoveReactionAsync(CustomEmojis.Loading.ToEmote(), Context.Client.CurrentUser);
            }
        }

        [Command("feedbackreply"), Remarks("userId message —"), Summary("This is how Samrux replies to feedback. Developer only.")]
        public async Task ReplyFeedback(ulong id, [Remainder]string message)
        {
            try
            {
                await Context.Client.GetUser(id).SendMessageAsync("```diff\n+The following message was sent in response to your recent feedback.\n-To reply to this message, use the 'feedback' command again.```\n" + message);
                await Context.Message.AddReactionAsync(CustomEmojis.Check.ToEmote());
            }
            catch (Exception e)
            {
                await logger.Log(LogSeverity.Verbose, $"{e}");
                await ReplyAsync($"```{e.Message}```");
                await Context.Message.AddReactionAsync(CustomEmojis.Cross.ToEmote());
            }
        }

        [Command("garbagecollect"), Alias("gc"), Summary("Clears unused memory if possible. Developer only.")]
        public async Task DoGarbageCollect()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            await Context.Message.AddReactionAsync(CustomEmojis.Check.ToEmote());
        }

        [Command("file"), Alias("readfile"), Remarks("startpoint endpoint filename —"), Summary("Sends the contents of a file in the bot's host location. Developer only.")]
        public async Task ReadFile(int start, int end, [Remainder]string file)
        {
            try
            {
                string fileText = File.ReadAllText(file);
                string message = $"```{"cs".If(file.Contains(".cs"))}\n{fileText.Substring(start, Math.Min(end, fileText.Length) - start - 11).Replace("```", "`​``")}```";
                await ReplyAsync(message);
            }
            catch (Exception e)
            {
                await logger.Log(LogSeverity.Debug, $"{e}");
                await ReplyAsync($"```{e.Message}```");
            }
        }

        [Command("getguilds"), Summary("Gets a list of guilds and member counts where this bot is in. Developer only.")]
        public async Task GetGuildMembers()
        {
            await ReplyAsync(string.Join("\n", Context.Client.Guilds.OrderByDescending(g => g.MemberCount).Select(g => $"{g.Name}: {g.MemberCount}")).Truncate(2000));
        }
    }
}