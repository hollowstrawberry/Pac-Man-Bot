using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Net;
using PacManBot.Games;
using PacManBot.Services;
using PacManBot.Constants;
using PacManBot.Extensions;
using PacManBot.Services.Database;

namespace PacManBot.Commands.Modules
{
    [Name("Developer"), Remarks("0")]
    [RequireOwner, BetterRequireBotPermission(ChannelPermission.AddReactions)]
    public class DevModule : BaseCustomModule
    {
        public BotConfig Config { get; }
        public ScriptingService Scripting { get; }
        public PacManDbContext Db { get; private set; }

        public DevModule(IServiceProvider services) : base(services)
        {
            Config = services.Get<BotConfig>();
            Scripting = services.Get<ScriptingService>();
        }



        [Command("setusername"), HideHelp]
        [Summary("Set the bot's username in all servers.")]
        public async Task SetUsername([Remainder]string name)
        {
            try
            {
                await Context.Client.CurrentUser.ModifyAsync(x => x.Username = name, DefaultOptions);
                await AutoReactAsync();
            }
            catch (Exception)
            {
                await AutoReactAsync(false);
                throw;
            }
        }


        [Command("setnickname"), HideHelp]
        [Summary("Set the bot's nickname in this server.")]
        [RequireContext(ContextType.Guild)]
        public async Task SetNickname([Remainder]string name = null)
        {
            try
            {
                await Context.Guild.CurrentUser.ModifyAsync(x => x.Nickname = name, DefaultOptions);
                await AutoReactAsync();
            }
            catch (Exception)
            {
                await AutoReactAsync(false);
                throw;
            }
        }


        [Command("setavatar"), HideHelp]
        [Summary("Set the bot's avatar to a file.")]
        [RequireContext(ContextType.Guild)]
        public async Task SetAvatar([Remainder]string path)
        {
            try
            {
                await Context.Client.CurrentUser.ModifyAsync(x => x.Avatar = new Image(path), DefaultOptions);
                await AutoReactAsync();
            }
            catch (Exception)
            {
                await AutoReactAsync(false);
                throw;
            }
        }



        [Command("run"), Alias("eval", "runasync", "evalasync"), HideHelp]
        [Summary("Run code, super dangerous do not try at home. Developer only.")]
        public async Task ScriptEval([Remainder]string code)
        {
            code = code.Trim(' ', '`', '\n');
            if (code.StartsWith("cs\n")) code = code.Remove(0, 3); // C# code block in Discord

            var dbProperty = typeof(StorageService).GetProperty("Db", BindingFlags.NonPublic | BindingFlags.Instance);
            Db = (PacManDbContext)dbProperty.GetValue(Storage); // If I need to access the database from a script

            await Context.Message.AddReactionAsync(CustomEmoji.ELoading, DefaultOptions);

            object result;
            try
            {
                result = await Scripting.EvalAsync(code, this);
                await Logger.Log(LogSeverity.Debug, LogSource.Eval, $"Successfully executed:\n {code}");
                await AutoReactAsync(true);
            }
            catch (Exception e)
            {
                result = e.Message;
                await Logger.Log(LogSeverity.Debug, LogSource.Eval, $"{e}");
                await AutoReactAsync(false);
            }

            await Context.Message.RemoveReactionAsync(CustomEmoji.ELoading, Context.Client.CurrentUser, DefaultOptions);
            if (result != null) await ReplyAsync($"```\n{result}".Truncate(1997) + "```");
        }


        [Command("feedbackreply"), Alias("reply"), HideHelp]
        [Summary("This is how Samrux replies to feedback. Developer only.")]
        public async Task ReplyFeedback(ulong useriD, [Remainder]string message)
        {
            try
            {
                await Context.Client.GetUser(useriD).SendMessageAsync(
                    "```diff\n+The following message was sent to you by this bot's owner." +
                    "\n-To reply to this message, use the 'feedback' command.```\n" + message,
                    options: DefaultOptions);
                await AutoReactAsync();
            }
            catch (Exception e)
            {
                await Logger.Log(LogSeverity.Debug, $"{e.Message}");
                await AutoReactAsync(false);
                await ReplyAsync($"```{e.Message}```");
            }
        }


        [Command("reloadcontent"), Alias("reload"), HideHelp]
        [Summary("Reloads the content.bot file. Developer only")]
        public async Task ReloadContent()
        {
            try
            {
                Config.LoadContent(File.ReadAllText(Files.Contents));
                await Logger.Log(LogSeverity.Info, "Reloaded bot content");
                await AutoReactAsync();
            }
            catch (Exception e)
            {
                await Logger.Log(LogSeverity.Error, $"Failed to load bot content: {e}");
                await ReplyAsync($"```{e.Message}```");
            }
        }


        [Command("log"), HideHelp]
        [Summary("Stores an entry in the bot logs. Developer only")]
        public async Task LogSomething([Remainder]string message)
        {
            await Logger.Log(LogSeverity.Info, LogSource.Owner, message);
            await AutoReactAsync();
        }


        [Command("garbagecollect"), Alias("gc"), HideHelp]
        [Summary("Clears unused memory if possible. Developer only.")]
        public async Task DoGarbageCollect()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            await AutoReactAsync();
        }


        [Command("file"), Alias("readfile"), HideHelp, Parameters("[start] [length] <file>")]
        [Summary("Sends the contents of a file in the bot's host location. Developer only.")]
        public async Task ReadFile(int start, int length, [Remainder]string filename)
        {
            try
            {
                string content = File.ReadAllText(filename).Replace("```", "`​``").Substring(start).Truncate(length);
                await ReplyAsync($"```{filename.Split('.').Last()}\n{content}".Truncate(1997) + "```");
            }
            catch (Exception e)
            {
                await Logger.Log(LogSeverity.Debug, $"{e.Message}");
                await ReplyAsync($"```{e.Message}```");
            }
        }

        [Command("file"), Alias("readfile"), HideHelp]
        public async Task ReadFile(int start, [Remainder]string file)
            => await ReadFile(start, 2000, file);

        [Command("file"), Alias("readfile"), HideHelp]
        public async Task ReadFile([Remainder]string file)
            => await ReadFile(0, 2000, file);


        [Command("getguilds"), Alias("guilds"), HideHelp]
        [Summary("Gets a list of guilds and member counts where this bot is in. Developer only.")]
        public async Task GetGuildMembers()
        {
            var guilds = Context.Client.Guilds.OrderByDescending(g => g.MemberCount);
            await ReplyAsync(guilds.Select(g => $"{g.Name}: {g.MemberCount}").JoinString("\n").Truncate(2000));
        }


        [Command("sudo clear"), Alias("sudoclear", "sudo cl", "wipe"), HideHelp]
        [Summary("Clear all messages in a range. Developer only.")]
        [BetterRequireBotPermission(ChannelPermission.ReadMessageHistory | ChannelPermission.ManageMessages)]
        public async Task ClearAllMessages(int amount = 10)
        {
            foreach (var message in await Context.Channel
                .GetMessagesAsync(amount, options: DefaultOptions)
                .FlattenAsync())
            {
                try
                {
                    await message.DeleteAsync(DefaultOptions);
                }
                catch (HttpException e)
                {
                    await Logger.Log(LogSeverity.Warning,
                                     $"Couldn't delete message {message.Id} in {Context.Channel.FullName()}: {e.Message}");
                }
            }
        }


        [Command("sudo say"), Alias("sudosay"), HideHelp]
        [Summary("Say anything. Developer-only version.")]
        public async Task ClearGameMessages([Remainder]string message)
        {
            await ReplyAsync(message);
        }


        [Command("deusexmachina"), Alias("deus", "domoves"), HideHelp]
        [Summary("Execute game moves in sequence regardless of turn or dignity. Developer only.")]
        public async Task DoRemoteGameMoves(params string[] moves)
        {
            var game = Games.GetForChannel<IMessagesGame>(Context.Channel.Id);
            if (game == null)
            {
                await ReplyAsync("How about you start a game first");
                return;
            }

            bool success = true;
            foreach (string move in moves)
            {
                try
                {
                    if (game.State == State.Active) game.Input(move);
                    else break;
                }
                catch (Exception e)
                {
                    await Logger.Log(LogSeverity.Debug, $"While executing debug game input: {e.Message}");
                    success = false;
                }
            }

            var msg = await game.GetMessage();
            if (msg != null) await msg.ModifyAsync(game.GetMessageUpdate(), DefaultOptions);
            else msg = await ReplyAsync(game.GetContent(), game.GetEmbed());

            if (game is MultiplayerGame tpGame && tpGame.BotTurn)
            {
                tpGame.BotInput();
                await Task.Delay(1000);
                await msg.ModifyAsync(game.GetMessageUpdate(), DefaultOptions);
            }

            if (game.State != State.Active) Games.Remove(game);

            await AutoReactAsync(success);
        }


        [Command("throw"), HideHelp]
        [Summary("Why would you do this")]
        public Task ThrowException()
        {
            throw new Exception("Accuracy roll: 20 | Successful throw");
        }
    }
}
