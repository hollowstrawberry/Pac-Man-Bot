using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Net;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PacManBot.Constants;
using PacManBot.Extensions;
using PacManBot.Games;
using PacManBot.Services;
using PacManBot.Services.Database;
using PacManBot.Utils;

namespace PacManBot.Commands.Modules
{
    [Name(ModuleNames.Dev), Remarks("0")]
    [RequireDeveloper]
    public class DevModule : BasePmBotModule
    {
        public PmBot Bot { get; set; }
        public GameService Games { get; set; }
        public ScriptingService Scripting { get; set; }
        public IServiceProvider Services { get; set; }


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

            await RespondAsync(embed);
        }


        [Command("$restart"), Alias("$shutdown"), HideHelp]
        [Summary("Shuts down the bot. Developer only.")]
        public async Task ShutDown()
        {
            var message = await RespondAsync(CustomEmoji.Loading);
            File.WriteAllText(Files.ManualRestart, $"{message.Channel.Id}/{message.Id}");

            Log.Info("Restarting", LogSource.Owner);
            await Bot.StopAsync();
            Environment.Exit(ExitCodes.ManualReboot);
        }


        [Command("$update"), HideHelp]
        [Summary("Perform a `git pull` and close the bot. Developer only.")]
        public async Task UpdateAndShutDown()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                await RespondAsync("This command is currently only available on Linux systems.");
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

            await RespondAsync($"```\n{result.Truncate(1990)}```");

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
            catch
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
            catch
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
            catch
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
                Log.Debug($"Successfully executed:\n {code}", LogSource.Eval);
                await AutoReactAsync(true);
            }
            catch (Exception e)
            {
                result = e.Message;
                Log.Debug($"{e}", LogSource.Eval);
                await AutoReactAsync(false);
            }

            await Context.Message.RemoveReactionAsync(CustomEmoji.ELoading, Context.Client.CurrentUser, DefaultOptions);
            if (result != null) await RespondAsync($"```\n{result.ToString().Truncate(1990)}```");
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
                using (var db = new PacManDbContext(Bot.Config.dbConnectionString))
                {
                    using (var command = db.Database.GetDbConnection().CreateCommand())
                    {
                        command.CommandText = query;
                        db.Database.OpenConnection();
                        using (var result = command.ExecuteReader())
                        {
                            affected = result.RecordsAffected;
                            while (result.Read())
                            {
                                object[] values = new object[result.FieldCount];
                                result.GetValues(values);

                                for (int i = 0; i < values.Length; i++)
                                    if (values[i] is string str && str.ContainsAny(" ", "\n"))
                                        values[i] = $"\"{values[i]}\"";

                                table.AppendLine(values?.JoinString("  "));
                            }
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
            await RespondAsync(message);
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
            await RespondAsync(message.Length == 0 ? "*No output*" : $"```\n{message.ToString().Truncate(1990)}```");
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
                Log.Debug($"{e.Message}");
                await AutoReactAsync(false);
                await RespondAsync($"```{e.Message}```");
            }
        }


        [Command("reloadcontent"), Alias("reload"), HideHelp]
        [Summary("Reloads the content.bot file. Developer only")]
        public async Task ReloadContent()
        {
            try
            {
                Bot.Config.LoadContent(File.ReadAllText(Files.Contents));
            }
            catch (Exception e)
            {
                Log.Error($"Failed to load bot content: {e}");
                await RespondAsync($"```{e.Message}```");
            }

            Log.Info("Reloaded bot content");
            await AutoReactAsync();
        }


        [Command("log"), HideHelp]
        [Summary("Stores an entry in the bot logs. Developer only")]
        public async Task DoLog([Remainder]string message)
        {
            Log.Info(message, LogSource.Owner);
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
            var emotes = Context.Guild.Emotes
                .OrderBy(x => x.Animated)
                .Select(x => x.Mention());

            await RespondAsync($"```{emotes.JoinString("\n")}```".Truncate(2000));
        }


        [Command("file"), Alias("readfile"), HideHelp, Parameters("[start] [length] <file>")]
        [Summary("Sends the contents of a file in the bot's host location. Developer only.")]
        public async Task ReadFile(int start, int length, [Remainder]string filename)
        {
            try
            {
                string content = File.ReadAllText(filename).Replace("```", "`​``").Substring(start).Truncate(length);
                content = content.Replace(Config.discordToken, ""); // Can't be too safe
                await RespondAsync($"```{filename.Split('.').Last()}\n{content}".Truncate(1997) + "```");
            }
            catch (Exception e)
            {
                Log.Debug($"Reading file {filename}: {e.Message}");
                await RespondAsync($"```Reading file {filename}: {e.Message}```");
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
            await RespondAsync(guilds.Select(g => $"{g.Name}: {g.MemberCount}").JoinString("\n").Truncate(2000));
        }


        [Command("sudo clear"), Alias("sudoclear", "sudo cl", "wipe"), HideHelp]
        [Summary("Clear all messages in a range. Developer only.")]
        [PmRequireBotPermission(ChannelPermission.ReadMessageHistory | ChannelPermission.ManageMessages)]
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
                    Log.Verbose($"Couldn't delete message {message.Id} in {Context.Channel.FullName()}: {e.Message}");
                }
            }
        }


        [Command("sudo say"), Alias("sudosay"), HideHelp]
        [Summary("Say anything. Developer-only version.")]
        public async Task ClearGameMessages([Remainder]string message)
        {
            await RespondAsync(message);
        }


        [Command("deusexmachina"), Alias("deus", "domoves"), HideHelp]
        [Summary("Execute game moves in sequence regardless of turn or dignity. Developer only.")]
        public async Task DoRemoteGameMoves(params string[] moves)
        {
            var game = Games.GetForChannel<IMessagesGame>(Context.Channel.Id);
            if (game == null)
            {
                await RespondAsync("How about you start a game first");
                return;
            }

            bool success = true;
            foreach (string move in moves)
            {
                try
                {
                    if (game.State == GameState.Active) await game.InputAsync(move);
                    else break;
                }
                catch (Exception e)
                {
                    Log.Debug($"While executing debug game input: {e}");
                    success = false;
                }
            }

            var msg = await game.GetMessageAsync();
            if (msg != null) await msg.ModifyAsync(game.GetMessageUpdate(), DefaultOptions);
            else msg = await ReplyAsync(game.GetContent(), game.GetEmbed());

            if (game is MultiplayerGame tpGame && tpGame.BotTurn)
            {
                await tpGame.BotInputAsync();
                await Task.Delay(1000);
                await msg.ModifyAsync(game.GetMessageUpdate(), DefaultOptions);
            }

            if (game.State != GameState.Active) Games.Remove(game);

            await AutoReactAsync(success);
        }


        [Command("throw"), HideHelp]
        [Summary("Throws an error. Developer only.")]
        public async Task ThrowError(bool arg = true)
        {
            if (arg) throw new Exception("oops");
            else await RespondAsync("no");
        }
    }
}
