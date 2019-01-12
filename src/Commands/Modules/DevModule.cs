using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Discord;
using Discord.Net;
using Discord.Commands;
using PacManBot.Games;
using PacManBot.Utils;
using PacManBot.Services;
using PacManBot.Services.Database;
using PacManBot.Constants;
using PacManBot.Extensions;

namespace PacManBot.Commands.Modules
{
    [Name(CustomEmoji.Discord + "Developer"), Remarks("0")]
    [RequireDeveloper, BetterRequireBotPermission(ChannelPermission.AddReactions)]
    public class DevModule : BaseCustomModule
    {
        private BotConfig internalConfig;
        private ScriptingService internalScripting;
        private PacManDbContext internalDb;

        public BotConfig Config => internalConfig ?? (internalConfig = Services.Get<BotConfig>());
        public ScriptingService Scripting => internalScripting ?? (internalScripting = Services.Get<ScriptingService>());
        public PacManDbContext Db => internalDb ?? (internalDb = new PacManDbContext(Config.dbConnectionString));


        public DevModule(IServiceProvider services) : base(services) {}


        protected override void AfterExecute(CommandInfo command)
        {
            base.AfterExecute(command);
            internalDb?.Dispose();
        }




        [Command("dev"), Alias("devcommands"), Remarks("List developer commands")]
        [Summary("Lists developer commands. Developer only.")]
        public async Task ShowDevCommands()
        {
            var commands = typeof(DevModule).GetMethods()
                .Select(x => x.Get<CommandAttribute>()?.Text)
                .Where(x => x != null)
                .Distinct()
                .JoinString(", ");

            var embed = new EmbedBuilder
            {
                Title = $"{CustomEmoji.Staff} Developer Commands",
                Color = Colors.PacManYellow,
                Description = commands
            };

            await ReplyAsync(embed);
        }


        [Command("$restart"), Alias("$shutdown"), HideHelp]
        [Summary("Stops all input for a few seconds then shuts down the bot. Developer only.")]
        public async Task ShutDown()
        {
            Services.Get<InputService>().StopListening();
            await Logger.Log(LogSeverity.Info, LogSource.Owner, "Preparing to shut down.");

            // Waits a bit to finish up whatever it might be doing at the moment
            await Context.Message.AddReactionAsync(CustomEmoji.ELoading, Bot.DefaultOptions);
            await Task.Delay(5000);
            await AutoReactAsync();
            await Context.Message.RemoveReactionAsync(CustomEmoji.ELoading, Context.Client.CurrentUser, Bot.DefaultOptions);

            await Logger.Log(LogSeverity.Info, LogSource.Owner, "Shutting down.");
            Environment.Exit(ExitCodes.ManualReboot);
        }


        [Command("$update"), HideHelp]
        [Summary("Perform a `git pull` and close the bot. Developer only.")]
        public async Task UpdateAndShutDown()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                await ReplyAsync("This command is currently only available on Linux systems.");
                return;
            }


            var process = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"-c \"git pull\"",
                    WorkingDirectory = Environment.CurrentDirectory,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };

            process.Start();
            string result = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            bool updated = !result.Contains("Already up-to-date");

            if (!updated) await AutoReactAsync(false);

            await ReplyAsync($"```\n{result.Truncate(1990)}```");

            if (updated) await ShutDown();
        }


        [Command("setusername"), HideHelp]
        [Summary("Set the bot's username in all servers. Developer only.")]
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
        [Summary("Set the bot's nickname in this server. Developer only.")]
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
        [Summary("Set the bot's avatar to a file. Developer only.")]
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


        [Command("eval"), Alias("evalasync", "run", "runasync"), HideHelp]
        [Summary("Run code, super dangerous do not try at home. Developer only.")]
        public async Task ScriptEval([Remainder]string code)
        {
            code = code.ExtractCode();

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
            if (result != null) await ReplyAsync($"```\n{result.ToString().Truncate(1990)}```");
        }


        [Command("sql"), HideHelp]
        [Summary("Execute a raw SQL query on the database. Dangerous. Developer only.")]
        public async Task SqlQuery([Remainder]string query)
        {
            query = query.ExtractCode();

            bool success = true;
            var message = new StringBuilder();
            var table = new StringBuilder();
            int affected = -1;

            try
            {
                using (var command = Db.Database.GetDbConnection().CreateCommand())
                {
                    command.CommandText = query;
                    Db.Database.OpenConnection();
                    using (var result = command.ExecuteReader())
                    {
                        affected = result.RecordsAffected;
                        while (result.Read())
                        {
                            object[] values = new object[result.FieldCount];
                            result.GetValues(values);

                            for (int i = 0; i < values.Length; i++) // Adds quotes to strings
                            {
                                if (values[i] is string str && str.ContainsAny(" ", "\n")) values[i] = $"\"{values[i]}\"";
                            }

                            table.AppendLine(values?.JoinString("  "));
                        }
                    }
                }
            }
            catch (SqliteException e)
            {
                success = false;
                message.Append($"```{e.Message}```");
            }

            if (affected >= 0) message.Append($"`{affected} rows affected`\n");
            if (table.Length > 0) message.Append($"```{table.ToString().Truncate(1990 - message.Length)}```");

            await AutoReactAsync(success);
            await ReplyAsync(message);
        }


        [Command("bf"), Alias("brainf", "brainfuck"), HideHelp]
        [Summary("Run a program in the Brainfuck language. Separate code and input with an exclamation point. Developer only.")]
        public async Task RunBrainf([Remainder]string userInput)
        {
            await Context.Message.AddReactionAsync(CustomEmoji.ELoading, DefaultOptions);

            var slice = userInput.ExtractCode().Split('!', 2);
            string program = slice[0];
            string programInput = slice.Length > 1 ? slice[1] : "";

            var message = new StringBuilder();
            var bf = new Brainfuck(program, programInput);

            try
            {
                message.Append(await bf.RunAsync(3000));
                await AutoReactAsync(true);
            }
            catch (Exception e)
            {
                switch (e)
                {
                    case Brainfuck.BrainfuckException be:
                        message.AppendLine($"Runtime exception: {be.Message}");
                        if (e.InnerException != null) message.AppendLine($"Inner exception: {e.InnerException}\n");
                        message.AppendLine($"Memory at this point: {be.Memory}\n");
                        message.AppendLine($"Output up to this point:\n{bf.Output}");
                        break;

                    case ArgumentException ae:
                        message.AppendLine("The provided program has invalid syntax.");
                        break;

                    default:
                        throw e;
                }
                await AutoReactAsync(false);
            }

            await Context.Message.RemoveReactionAsync(CustomEmoji.ELoading, Context.Client.CurrentUser, DefaultOptions);
            await ReplyAsync(message.Length == 0 ? "*No output*" : $"```\n{message.ToString().Truncate(1990)}```");
        }


        [Command("feedbackreply"), Alias("reply"), HideHelp]
        [Summary("This is how Samrux replies to feedback. Developer only.")]
        public async Task ReplyFeedback(ulong userId, [Remainder]string message)
        {
            try
            {
                string pre = "```diff\n+The following message was sent to you by this bot's owner." +
                             "\n-To reply to this message, use the 'feedback' command.```\n";

                await Context.Client.GetUser(userId).SendMessageAsync(pre + message, options: DefaultOptions);
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
        public async Task DoLog([Remainder]string message)
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


        [Command("emotes"), Alias("showemotes"), HideHelp]
        [Summary("Sends all emote codes in this server. Developer only.")]
        [RequireContext(ContextType.Guild)]
        public async Task SendServerEmotes()
        {
            string emotes = Context.Guild.Emotes
                .OrderBy(x => x.Animated)
                .Select(x => x.Mention())
                .JoinString("\n");

            await ReplyAsync($"```{emotes}```");
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
        [Summary("Gets a list of top 100 guilds and member counts where this bot is in. Developer only.")]
        public async Task GetGuildMembers()
        {
            var guilds = Context.Client.Guilds.OrderByDescending(g => g.MemberCount).Take(100);
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
    }
}
