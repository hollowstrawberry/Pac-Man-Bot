using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using PacManBot.Constants;
using PacManBot.Extensions;
using PacManBot.Games;
using PacManBot.Services.Database;
using PacManBot.Utils;

namespace PacManBot.Commands.Modules
{
    [Category(Categories.Dev)]
    [RequireOwner, Hidden]
    [RequireBotPermissions(BaseBotPermissions)]
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Command reflection")]
    public class OwnerModule : BaseModule
    {
        public IHostApplicationLifetime App { get; set; }
        
        private readonly ScriptOptions _scriptOptions = ScriptOptions.Default
            .WithImports("Microsoft.EntityFrameworkCore", "Newtonsoft.Json",
                "System", "System.IO", "System.Text", "System.Linq", "System.Reflection", "System.Diagnostics", "System.Threading.Tasks", "System.Collections.Generic", "System.Text.RegularExpressions",
                "DSharpPlus", "DSharpPlus.CommandsNext", "DSharpPlus.Entities", "DSharpPlus.Exceptions",
                "PacManBot", "PacManBot.Constants", "PacManBot.Utils", "PacManBot.Extensions", "PacManBot.Games", "PacManBot.Games.Concrete", "PacManBot.Games.Concrete.Rpg", "PacManBot.Commands", "PacManBot.Commands.Modules", "PacManBot.Services", "PacManBot.Services.Database"
            )
            .WithReferences(AppDomain.CurrentDomain.GetAssemblies().Where(xa => !xa.IsDynamic && !string.IsNullOrWhiteSpace(xa.Location)));



        [Command("$restart"), Aliases("$shutdown"), Hidden]
        [Description("Shuts down the bot. Developer only.")]
        public async Task ShutDown(CommandContext ctx)
        {
            var message = await ctx.RespondAsync(CustomEmoji.Loading);
            File.WriteAllText(Files.ManualRestart, $"{message.Channel.Id}/{message.Id}");

            Log.Info("Restarting");
            App.StopApplication(ExitCodes.ManualReboot);
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

            bool updated = result.Contains("Updating");

            if (!updated) await ctx.AutoReactAsync(false);

            await ctx.RespondAsync($"```\n{result.Truncate(1990)}```");

            if (updated) await ShutDown(ctx);
        }


        [Command("setstatus"), Hidden]
        [Description("Set the bot's status in all shards")]
        public async Task SetStatus(CommandContext ctx, ActivityType type, [RemainingText]string status)
        {
            
            await ShardedClient.UpdateStatusAsync(new DiscordActivity(status, type));
            await ctx.AutoReactAsync();
        }


        [Command("setstatus"), Hidden]
        [Description("Set the bot's status in all shards")]
        public async Task SetStatus(CommandContext ctx, UserStatus status)
        {

            await ShardedClient.UpdateStatusAsync(userStatus: status);
            await ctx.AutoReactAsync();
        }


        /// <summary>Used inside evaluated scripts</summary>
        public CommandContext ctx;

        [Command("eval"), Aliases("evalasync", "run", "runasync"), Hidden]
        [Description("Run code, super dangerous do not try at home. Developer only.")]
        public async Task ScriptEval(CommandContext ctx, [RemainingText]string code)
        {
            code = code.ExtractCode();
            this.ctx = ctx;
            await ctx.Message.CreateReactionAsync(CustomEmoji.ELoading);

            try
            {
                using var eval = CSharpScript.EvaluateAsync(code, _scriptOptions, this);
                var result = await eval;
                Log.Info($"Successfully executed:\n {code}");
                await ctx.AutoReactAsync(true);
                if (result is not null) await ctx.RespondAsync($"```\n{result.ToString().Truncate(1990)}```");
            }
            catch (Exception e)
            {
                Log.Info($"Eval - {e}");
                await ctx.AutoReactAsync(false);
                await ctx.RespondAsync($"```\n{e.Message}```");
            }
            finally
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }

            await ctx.Message.DeleteOwnReactionAsync(CustomEmoji.ELoading);
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
                using var db = new PacManDbContext(Config.dbConnectionString);
                using var command = db.Database.GetDbConnection().CreateCommand();
                command.CommandText = query;
                db.Database.OpenConnection();
                using var result = command.ExecuteReader();
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
                        if (e.InnerException is not null) message.AppendLine($"Inner exception: {e.InnerException}\n");
                        message.AppendLine($"Memory at this point: {be.Memory}\n");
                        message.AppendLine($"Output up to this point:\n{bf.Output}");
                        break;

                    case ArgumentException _:
                        message.AppendLine("The provided program has invalid syntax.");
                        break;

                    default:
                        throw;
                }
                await ctx.AutoReactAsync(false);
            }

            await ctx.Message.DeleteOwnReactionAsync(CustomEmoji.ELoading);
            await ctx.RespondAsync(message.Length == 0 ? "*No output*" : $"```\n{message.ToString().Truncate(1990)}```");
        }


        [Command("feedbackreply"), Aliases("reply"), Hidden]
        [Description("This is how the owner replies to feedback. Developer only.")]
        public async Task ReplyFeedback(CommandContext ctx, ulong userId, [RemainingText]string message)
        {
            try
            {
                string pre = "```diff\n+The following message was sent to you by this bot's owner." +
                             "\n-To reply to this message, use the 'feedback' command.```\n";

                var member = ShardedClient.GetMember(userId);
                if (member is null)
                {
                    await ctx.AutoReactAsync(false);
                }
                else
                {
                    await member.SendMessageAsync(pre + message);
                    await ctx.AutoReactAsync();
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
            Log.Info(message);
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
                string content = File.ReadAllText(filename).Replace("```", "`​``")[start..].Truncate(length);
                content = content.Replace(Config.discordToken, ""); // Can't be too safe
                if (Config.discordBotListToken is not null) content = content.Replace(Config.discordBotListToken, "");
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
            if (game is null)
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

            await game.UpdateMessageAsync();

            if (game is MultiplayerGame mgame && await mgame.IsBotTurnAsync())
            {
                await mgame.BotInputAsync();
                await Task.Delay(1000);
                await mgame.UpdateMessageAsync();
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


        [Command("shardstatus"), Hidden]
        [Description("Check status of all shards")]
        public async Task ShardStatus(CommandContext ctx)
        {
            var sb = new StringBuilder();
            foreach (var shard in ShardedClient.ShardClients.Values)
            {
                var app = await shard.GetCurrentApplicationAsync();
                var owner = app is null ? null : await shard.GetUserAsync(app.Owners.First().Id);
                sb.Append($"(#{shard.ShardId + 1}: `{shard.Ping}ms`{" `app`".If(app is not null)}{" `owner`".If(owner is not null)}) ");
            }
            await ctx.RespondAsync(sb.ToString());
        }


        [Command("shardretry"), Hidden]
        [Description("Resusbcribe each shard to events from discord")]
        public async Task ShardRetry(CommandContext ctx)
        {
            foreach (var shard in ShardedClient.ShardClients.Values)
            {
                Input.StopListening(shard);
                Input.StartListening(shard);
            }

            await ctx.AutoReactAsync();
        }
    }
}
