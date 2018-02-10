using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using PacManBot.Services;
using PacManBot.Constants;
using PacManBot.Modules.PacMan;

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

        [Command("run"), Alias("eval", "runasync", "evalasync"), Summary("Run code, super dangerous do not try at home. Developers only.")]
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

        [Command("feedbackreply"), Summary("This is how Samrux replies to feedback.")]
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

        [Command("garbagecollect"), Alias("gc"), Summary("The garbage truck is here")]
        public async Task DoGarbageCollect()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            await Context.Message.AddReactionAsync(CustomEmojis.Check.ToEmote());
        }
    }
}
