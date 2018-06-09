using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using PacManBot.Services;
using PacManBot.Constants;
using Discord.Net;
using PacManBot.Games;
using static PacManBot.Games.GameUtils;

namespace PacManBot.Modules
{
    [Name("Developer"), Remarks("0")]
    [RequireOwner, BetterRequireBotPermission(ChannelPermission.AddReactions)]
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
                await Context.Message.AddReactionAsync(CustomEmoji.ELoading, options: Utils.DefaultOptions);
                await scripting.Eval(code, new ShardedCommandContext(shardedClient, Context.Message));
                await Context.Message.AddReactionAsync(CustomEmoji.ECheck, options: Utils.DefaultOptions);
            }
            catch (Exception e)
            {
                await ReplyAsync($"```cs\n{e.Message}```");
                await logger.Log(LogSeverity.Debug, "Eval", $"{e}");
                await Context.Message.AddReactionAsync(CustomEmoji.ECross, Utils.DefaultOptions);
            }
            finally
            {
                await Context.Message.RemoveReactionAsync(CustomEmoji.ELoading, Context.Client.CurrentUser, Utils.DefaultOptions);
            }
        }


        [Command("feedbackreply"), Alias("reply"), HideHelp]
        [Summary("This is how Samrux replies to feedback. Developer only.")]
        public async Task ReplyFeedback(ulong useriD, [Remainder]string message)
        {
            try
            {
                await shardedClient.GetUser(useriD).SendMessageAsync("```diff\n+The following message was sent to you by this bot's owner." +
                                                                     "\n-To reply to this message, use the 'feedback' command.```\n" + message);
                await Context.Message.AddReactionAsync(CustomEmoji.ECheck, options: Utils.DefaultOptions);
            }
            catch (Exception e)
            {
                await logger.Log(LogSeverity.Debug, $"{e.Message}");
                await ReplyAsync($"```{e.Message}```", options: Utils.DefaultOptions);
                await Context.Message.AddReactionAsync(CustomEmoji.ECross, Utils.DefaultOptions);
            }
        }


        [Command("reloadcontent"), Alias("reload"), HideHelp]
        [Summary("Reloads the content.bot file. Developer only")]
        public async Task ReloadContent()
        {
            try
            {
                storage.LoadBotContent();
            }
            catch (Exception e)
            {
                await logger.Log(LogSeverity.Error, $"{e}");
                await ReplyAsync(e.Message, options: Utils.DefaultOptions);
                return;
            }

            await logger.Log(LogSeverity.Info, "Reloaded bot content");
            await Context.Message.AddReactionAsync(CustomEmoji.ECheck, Utils.DefaultOptions);
        }


        [Command("log"), HideHelp]
        [Summary("Stores an entry in the bot logs. Developer only")]
        public async Task LogSomething([Remainder]string message)
        {
            await logger.Log(LogSeverity.Info, LogSource.Owner, message);
            await Context.Message.AddReactionAsync(CustomEmoji.ECheck, Utils.DefaultOptions);
        }


        [Command("garbagecollect"), Alias("gc"), HideHelp]
        [Summary("Clears unused memory if possible. Developer only.")]
        public async Task DoGarbageCollect()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            await Context.Message.AddReactionAsync(CustomEmoji.ECheck, Utils.DefaultOptions);
        }


        [Command("file"), Alias("readfile"), HideHelp, Parameters("[start] [length] <file>")]
        [Summary("Sends the contents of a file in the bot's host location. Developer only.")]
        public async Task ReadFile(int start, int length, [Remainder]string filename)
        {
            try
            {
                await ReplyAsync($"```{filename.Split('.').Last()}\n{File.ReadAllText(filename).Replace("```", "`​``").Substring(start).Truncate(length)}".Truncate(1997) + "```", options: Utils.DefaultOptions);
            }
            catch (Exception e)
            {
                await logger.Log(LogSeverity.Debug, $"{e.Message}");
                await ReplyAsync($"```{e.Message}```", options: Utils.DefaultOptions);
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
            await ReplyAsync(string.Join("\n", shardedClient.Guilds.OrderByDescending(g => g.MemberCount).Select(g => $"{g.Name}: {g.MemberCount}")).Truncate(2000), options: Utils.DefaultOptions);
        }


        [Command("sudo clear"), Alias("sudoclear", "sudo cl", "wipe"), HideHelp]
        [Summary("Clear all messages in a range. Developer only.")]
        [BetterRequireBotPermission(ChannelPermission.ReadMessageHistory | ChannelPermission.ManageMessages)]
        public async Task ClearAllMessages(int amount = 10)
        {
            foreach (IMessage message in await Context.Channel.GetMessagesAsync(amount).FlattenAsync())
            {
                try
                {
                    await message.DeleteAsync(Utils.DefaultOptions);
                }
                catch (HttpException e)
                {
                    await logger.Log(LogSeverity.Warning, $"Couldn't delete message {message.Id} in {Context.Channel.FullName()}: {e.Message}");
                }
            }
        }


        [Command("sudo say"), Alias("sudosay"), HideHelp]
        [Summary("Say anything. Developer-only version.")]
        public async Task ClearGameMessages([Remainder]string text)
        {
            await ReplyAsync(text, options: Utils.DefaultOptions);
        }


        [Command("deusexmachina"), Alias("deus", "domoves"), HideHelp]
        [Summary("Execute game moves in sequence regardless of turn or dignity. Developer only.")]
        public async Task DoRemoteGameMoves(params string[] moves)
        {
            var game = storage.GetGame<IMessagesGame>(Context.Channel.Id);
            if (game == null)
            {
                await ReplyAsync("How about you start a game first", options: Utils.DefaultOptions);
                return;
            }

            bool success = true;
            foreach (string move in moves)
            {
                try
                {
                    if (game.State == State.Active) game.DoTurn(move);
                    else break;
                }
                catch (Exception e)
                {
                    await logger.Log(LogSeverity.Debug, e.Message);
                    success = false;
                }
            }

            var msg = await game.GetMessage();
            if (msg != null) await msg.ModifyAsync(game.UpdateMessage, Utils.DefaultOptions);
            else msg = await ReplyAsync(game.GetContent(), false, game.GetEmbed()?.Build(), Utils.DefaultOptions);

            if (game is MultiplayerGame tpGame && tpGame.AITurn)
            {
                tpGame.DoTurnAI();
                await Task.Delay(1000);
                await msg.ModifyAsync(game.UpdateMessage, Utils.DefaultOptions);
            }

            if (game.State != State.Active) storage.DeleteGame(game);

            await Context.Message.AddReactionAsync(success ? CustomEmoji.ECheck : CustomEmoji.ECross, Utils.DefaultOptions); 
        }


        [Command("throw"), HideHelp]
        [Summary("Why would you do this")]
        public Task ThrowException()
        {
            throw new Exception("Accuracy roll: 20 | Successful throw");
        }
    }
}
