using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
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
    [Description(ModuleNames.Dev)]
    [RequireOwner]
    public class OwnerModule : BasePmBotModule
    {
        public PmBot Bot { get; set; }
        public ScriptingService Scripting { get; set; }


        [Command("dev"), Aliases("devcommands")]
        [Description("Lists developer commands. Developer only.")]
        public async Task ShowDevCommands(CommandContext ctx)
        {
            var commands = typeof(OwnerModule).GetMethods()
                .Select(x => x.Get<CommandAttribute>()?.Name)
                .Where(x => x != null)
                .Distinct()
                .JoinString(", ");

            var embed = new DiscordEmbedBuilder
            {
                Title = $"{CustomEmoji.Staff} Developer Commands",
                Color = Colors.PacManYellow,
                Description = commands
            };

            await ctx.RespondAsync(embed);
        }


        [Command("$restart"), Aliases("$shutdown"), Hidden]
        [Description("Shuts down the bot. Developer only.")]
        public async Task ShutDown(CommandContext ctx)
        {
            var message = await ctx.RespondAsync(CustomEmoji.Loading);
            File.WriteAllText(Files.ManualRestart, $"{message.Channel.Id}/{message.Id}");

            Log.Info("Restarting", LogSource.Owner);
            await Bot.StopAsync();
            Environment.Exit(ExitCodes.ManualReboot);
        }


        [Command("$update"), Hidden]
        [Description("Perform a `git pull` and close the bot. Developer only.")]
        public async Task UpdateAndShutDown(CommandContext ctx)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                await ctx.RespondAsync("This command is currently only available on Linux systems.");
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

            if (!updated) await ctx.AutoReactAsync(false);

            await ctx.RespondAsync($"```\n{result.Truncate(1990)}```");

            if (updated) await ShutDown(ctx);
        }

        public CommandContext ctx;

        [Command("eval"), Aliases("evalasync", "run", "runasync"), Hidden]
        [Description("Run code, super dangerous do not try at home. Developer only.")]
        public async Task ScriptEval(CommandContext ctx, [RemainingText]string code)
        {
            code = code.ExtractCode();

            await ctx.Message.CreateReactionAsync(CustomEmoji.ELoading);

            object result;
            try
            {
                this.ctx = ctx;
                result = await Scripting.EvalAsync(code, this);
                Log.Debug($"Successfully executed:\n {code}", LogSource.Eval);
                await ctx.AutoReactAsync(true);
            }
            catch (Exception e)
            {
                result = e.Message;
                Log.Debug($"{e}", LogSource.Eval);
                await ctx.AutoReactAsync(false);
            }

            await ctx.Message.DeleteOwnReactionAsync(CustomEmoji.ELoading);
            if (result != null) await ctx.RespondAsync($"```\n{result.ToString().Truncate(1990)}```");
        }


        [Command("sql"), Hidden]
        [Description("Execute a raw SQL query on the database. Dangerous. Developer only.")]
        public async Task SqlQuery(CommandContext ctx, [RemainingText]string query)
        {
            query = query.ExtractCode();

            bool success = true;
            var message = new StringBuilder();
            var table = new StringBuilder();
            int affected = -1;

            try
            {
                using (var db = new PacManDbContext(Config.dbConnectionString))
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

            await ctx.AutoReactAsync(success);
            await ctx.RespondAsync(message);
        }


        [Command("bf"), Aliases("brainf", "brainfuck"), Hidden]
        [Description("Run a program in the Brainfuck language. Separate code and input with an exclamation point. Developer only.")]
        public async Task RunBrainf(CommandContext ctx, [RemainingText]string userInput)
        {
            await ctx.Message.CreateReactionAsync(CustomEmoji.ELoading);

            var slice = userInput.ExtractCode().Split('!', 2);
            string program = slice[0];
            string programInput = slice.Length > 1 ? slice[1] : "";

            var message = new StringBuilder();
            var bf = new Brainfuck(program, programInput);

            try
            {
                message.Append(await bf.RunAsync(3000));
                await ctx.AutoReactAsync(true);
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

                    case ArgumentException _:
                        message.AppendLine("The provided program has invalid syntax.");
                        break;

                    default:
                        throw e;
                }
                await ctx.AutoReactAsync(false);
            }

            await ctx.Message.DeleteOwnReactionAsync(CustomEmoji.ELoading);
            await ctx.RespondAsync(message.Length == 0 ? "*No output*" : $"```\n{message.ToString().Truncate(1990)}```");
        }


        [Command("feedbackreply"), Aliases("reply"), Hidden]
        [Description("This is how Samrux replies to feedback. Developer only.")]
        public async Task ReplyFeedback(CommandContext ctx, ulong userId, [RemainingText]string message)
        {
            try
            {
                string pre = "```diff\n+The following message was sent to you by this bot's owner." +
                             "\n-To reply to this message, use the 'feedback' command.```\n";

                // this shouldn't be this complicated
                foreach (var shard in ShardedClient.ShardClients.Values)
                    foreach (var guild in shard.Guilds.Values)
                        if (guild.Members.TryGetValue(userId, out var member))
                        {
                            await member.SendMessageAsync(pre + message);
                            await ctx.AutoReactAsync();
                            return;
                        }
            }
            catch (Exception e)
            {
                Log.Debug($"{e.Message}");
                await ctx.AutoReactAsync(false);
                await ctx.RespondAsync($"```{e.Message}```");
            }
        }


        [Command("reloadcontent"), Aliases("reload"), Hidden]
        [Description("Reloads the content.bot file. Developer only")]
        public async Task ReloadContent(CommandContext ctx)
        {
            try
            {
                Config.LoadContent(File.ReadAllText(Files.Contents));
            }
            catch (Exception e)
            {
                Log.Error($"Failed to load bot content: {e}");
                await ctx.RespondAsync($"```{e.Message}```");
            }

            Log.Info("Reloaded bot content");
            await ctx.AutoReactAsync();
        }


        [Command("log"), Hidden]
        [Description("Stores an entry in the bot logs. Developer only")]
        public async Task DoLog(CommandContext ctx, [RemainingText]string message)
        {
            Log.Info(message, LogSource.Owner);
            await ctx.AutoReactAsync();
        }


        [Command("garbagecollect"), Aliases("gc"), Hidden]
        [Description("Clears unused memory if possible. Developer only.")]
        public async Task DoGarbageCollect(CommandContext ctx)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            await ctx.AutoReactAsync();
        }


        [Command("emotes"), Aliases("showemotes"), Hidden]
        [Description("Sends all emote codes in this server. Developer only.")]
        [RequireGuild]
        public async Task SendServerEmotes(CommandContext ctx)
        {
            var emotes = ctx.Guild.Emojis.Values
                .OrderBy(x => x.IsAnimated)
                .Select(x => x.ToString());

            await ctx.RespondAsync($"```{emotes.JoinString("\n")}```".Truncate(2000));
        }


        [Command("file"), Aliases("readfile"), Hidden]
        [Description("Sends the contents of a file in the bot's host location. Developer only.")]
        public async Task ReadFile(CommandContext ctx, int start, int length, [RemainingText]string filename)
        {
            try
            {
                string content = File.ReadAllText(filename).Replace("```", "`​``").Substring(start).Truncate(length);
                content = content.Replace(Config.discordToken, ""); // Can't be too safe
                await ctx.RespondAsync($"```{filename.Split('.').Last()}\n{content}".Truncate(1997) + "```");
            }
            catch (Exception e)
            {
                Log.Debug($"Reading file {filename}: {e.Message}");
                await ctx.RespondAsync($"```Reading file {filename}: {e.Message}```");
            }
        }

        [Command("file"), Hidden]
        public async Task ReadFile(CommandContext ctx, int start, [RemainingText]string file)
            => await ReadFile(ctx, start, 2000, file);

        [Command("file"), Hidden]
        public async Task ReadFile(CommandContext ctx, [RemainingText]string file)
            => await ReadFile(ctx, 0, 2000, file);


        [Command("getguilds"), Aliases("guilds"), Hidden]
        [Description("Gets a list of top 100 guilds and member counts where this bot is in. Developer only.")]
        [RequireDirectMessage]
        public async Task GetGuildMembers(CommandContext ctx)
        {
            var guilds = ShardedClient.ShardClients.Values
                .SelectMany(x => x.Guilds.Values)
                .OrderByDescending(x => x.MemberCount).Take(100)
                .Select(g => $"{g.Name}: {g.MemberCount}");
            await ctx.RespondAsync(guilds.JoinString("\n").Truncate(2000));
        }


        [Command("sudoclear"), Aliases("wipe"), Hidden]
        [Description("Clear all messages in a range. Developer only.")]
        [RequireBotPermissions(Permissions.ReadMessageHistory | Permissions.ManageMessages)]
        public async Task ClearAllMessages(CommandContext ctx, int amount = 10)
        {
            if (amount < 1 || amount > 100)
            {
                await ctx.RespondAsync("bruh");
                return;
            }
            await ctx.Channel.DeleteMessagesAsync(await ctx.Channel.GetMessagesAsync(amount));
        }


        [Command("sudosay"), Hidden]
        [Description("Say anything. Developer-only version.")]
        public async Task ClearGameMessages(CommandContext ctx, [RemainingText]string message)
        {
            await ctx.RespondAsync(message);
        }


        [Command("deusexmachina"), Aliases("deus", "domoves"), Hidden]
        [Description("Execute game moves in sequence regardless of turn or dignity. Developer only.")]
        public async Task DoRemoteGameMoves(CommandContext ctx, params string[] moves)
        {
            var game = Games.GetForChannel<IMessagesGame>(ctx.Channel.Id);
            if (game == null)
            {
                await ctx.RespondAsync("How about you start a game first");
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
            if (msg != null) await msg.ModifyWithGameAsync(game);
            else msg = await ctx.RespondAsync(await game.GetContentAsync(), await game.GetEmbedAsync());

            if (game is MultiplayerGame mgame && await mgame.IsBotTurnAsync())
            {
                await mgame.BotInputAsync();
                await Task.Delay(1000);
                await msg.ModifyWithGameAsync(mgame);
            }

            if (game.State != GameState.Active) Games.Remove(game);

            await ctx.AutoReactAsync(success);
        }


        [Command("throw"), Hidden]
        [Description("Throws an error. Developer only.")]
        public async Task ThrowError(CommandContext ctx, bool arg = true)
        {
            if (arg) throw new Exception("oops");
            else await ctx.RespondAsync("no");
        }
    }
}
