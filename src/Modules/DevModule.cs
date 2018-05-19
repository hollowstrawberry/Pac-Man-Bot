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
    [RequireOwner, RequireBotPermissionImproved(ChannelPermission.AddReactions)]
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
                await Context.Message.RemoveReactionAsync(CustomEmoji.Loading, Context.Client.CurrentUser, Utils.DefaultRequestOptions);
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
                await ReplyAsync($"```{e.Message}```", options: Utils.DefaultRequestOptions);
                await Context.Message.AddReactionAsync(CustomEmoji.Cross);
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
                await ReplyAsync(e.Message, options: Utils.DefaultRequestOptions);
                return;
            }

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
                await ReplyAsync($"```{"cs".If(filename.Contains(".cs"))}\n{File.ReadAllText(filename).Replace("```", "`​``").Substring(start).Truncate(length)}".Truncate(1997) + "```", options: Utils.DefaultRequestOptions);
            }
            catch (Exception e)
            {
                await logger.Log(LogSeverity.Debug, $"{e.Message}");
                await ReplyAsync($"```{e.Message}```", options: Utils.DefaultRequestOptions);
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
            await ReplyAsync(string.Join("\n", shardedClient.Guilds.OrderByDescending(g => g.MemberCount).Select(g => $"{g.Name}: {g.MemberCount}")).Truncate(2000), options: Utils.DefaultRequestOptions);
        }


        [Command("sudoclear"), Alias("wipe"), HideHelp]
        [Summary("Clear all messages in a range. Developer only.")]
        [RequireBotPermissionImproved(ChannelPermission.ReadMessageHistory | ChannelPermission.ManageMessages)]
        public async Task ClearGameMessages(int amount = 10)
        {
            foreach (IMessage message in await Context.Channel.GetMessagesAsync(amount).FlattenAsync())
            {
                try
                {
                    await message.DeleteAsync(Utils.DefaultRequestOptions);
                }
                catch (HttpException e)
                {
                    await logger.Log(LogSeverity.Warning, $"Couldn't delete message {message.Id} in {Context.Channel.FullName()}: {e.Message}");
                }
            }
        }


        [Command("deusexmachina"), Alias("deus", "domoves"), HideHelp]
        [Summary("Clear all messages in a range. Developer only.")]
        [RequireBotPermissionImproved(ChannelPermission.ReadMessageHistory | ChannelPermission.ManageMessages)]
        public async Task DoRemoteGameMoves(params string[] moves)
        {
            foreach (var game in storage.GameInstances.Where(g => !(g is PacManGame)))
            {
                if (game.channelId == Context.Channel.Id)
                {
                    bool success = true;
                    foreach (string move in moves)
                    {
                        if (game.IsInput(move) && game.state == State.Active) game.DoTurn(move);
                        else success = false;
                    }

                    var msg = await game.GetMessage();
                    if (msg != null) await msg.ModifyAsync(game.UpdateDisplay, Utils.DefaultRequestOptions);
                    else msg = await ReplyAsync(game.GetContent(), false, game.GetEmbed()?.Build(), Utils.DefaultRequestOptions);

                    if (game.AITurn)
                    {
                        game.DoTurnAI();
                        await Task.Delay(1000);
                        await msg.ModifyAsync(game.UpdateDisplay, Utils.DefaultRequestOptions);
                    }

                    if (game.state != State.Active) storage.DeleteGame(game);

                    await Context.Message.AddReactionAsync(success ? CustomEmoji.Check : CustomEmoji.Cross);
                    return;
                }
            }

            await ReplyAsync("How about you start a game first");
        }


        [Command("throw"), HideHelp]
        [Summary("Why would you do this")]
        public async Task ThrowException()
        {
            throw new Exception("Huh.");
        }
    }
}
