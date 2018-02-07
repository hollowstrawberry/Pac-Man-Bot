using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using PacManBot.Services;
using PacManBot.Constants;
using Discord.WebSocket;

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

        [Command("run"), Alias("eval"), Summary("Run code, super dangerous do not try at home")]
        public async Task Run([Remainder]string code)
        {
            try
            {
                await Context.Message.AddReactionAsync(CustomEmojis.Loading.ToEmote());
                await scripting.Eval(code, Context);
                await Context.Message.AddReactionAsync(CustomEmojis.Check.ToEmote());
            }
            catch (Exception exception)
            {
                await ReplyAsync($"```{exception.Message}```");
                await logger.Log(LogSeverity.Debug, $"{exception}");
                await Context.Message.AddReactionAsync(CustomEmojis.Cross.ToEmote());
            }
            finally
            {
                await Context.Message.RemoveReactionAsync(CustomEmojis.Loading.ToEmote(), Context.Client.CurrentUser);
            }
        }

        [Command("feedbackreply"), Summary("I could do this with eval but it's easier like this")]
        public async Task ReplyFeedback(ulong id, [Remainder]string message)
        {
            try
            {
                await Context.Client.GetUser(id).SendMessageAsync("```diff\n+The following message was sent in response to your recent feedback.\n-To reply to this message, use the 'feedback' command again.```\n" + message);
                await Context.Message.AddReactionAsync(CustomEmojis.Check.ToEmote());
            }
            catch (Exception e) {
                await logger.Log(LogSeverity.Verbose, $"{e}");
                await ReplyAsync($"```{e.Message}```");
                await Context.Message.AddReactionAsync(CustomEmojis.Cross.ToEmote());
            }
        }
    }
}
