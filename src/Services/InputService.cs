using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.CommandsNext.Exceptions;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.Exceptions;
using PacManBot.Extensions;
using PacManBot.Games;
using PacManBot.Games.Concrete;
using PacManBot.Services.Database;
using PacManBot.Utils;

namespace PacManBot.Services
{
    /// <summary>
    /// Handles all external input coming from Discord, using it for commands and games.
    /// </summary>
    public class InputService
    {
        private readonly DiscordShardedClient _client;
        private readonly DatabaseService _storage;
        private readonly LoggingService _log;
        private readonly GameService _games;

        private readonly ConcurrentDictionary<PendingResponse, byte> _pendingResponses;
        private readonly ConcurrentDictionary<ulong, DateTime> _lastGuildUsersDownload;

        private static readonly Regex StartsWithAnyMention = new Regex(@"^<(@|#|a?:)");
        
        private Regex _mentionPrefix = null;
        /// <summary>Is a match when the given text begins with a mention to the bot's current user.</summary>
        public Regex MentionPrefix => _mentionPrefix ??= new Regex($@"^<@!?{_client.CurrentUser.Id}>");


        public InputService(DiscordShardedClient client, LoggingService log,
            DatabaseService storage, GameService games)
        {
            _client = client;
            _storage = storage;
            _log = log;
            _games = games;

            _pendingResponses = new ConcurrentDictionary<PendingResponse, byte>();
            _lastGuildUsersDownload = new ConcurrentDictionary<ulong, DateTime>();
        }


        /// <summary>Start listening to input events from Discord.</summary>
        public void StartListening(DiscordClient shard)
        {
            shard.MessageCreated += OnMessageReceived;
            shard.MessageReactionAdded += OnReactionAdded;
            shard.MessageReactionRemoved += OnReactionRemoved;
            shard.GetCommandsNext().CommandErrored += OnCommandErrored;
            shard.GetCommandsNext().CommandExecuted += OnCommandExecuted;
        }


        /// <summary>Stop listening to input events from Discord.</summary>
        public void StopListening(DiscordClient shard)
        {
            shard.MessageCreated -= OnMessageReceived;
            shard.MessageReactionAdded -= OnReactionAdded;
            shard.MessageReactionRemoved -= OnReactionRemoved;
            shard.GetCommandsNext().CommandErrored -= OnCommandErrored;
            shard.GetCommandsNext().CommandExecuted -= OnCommandExecuted;
        }


        /// <summary>Returns the first new message that satisfies the given condition within 
        /// a timeout period in seconds, or null if no match is received.</summary>
        public async Task<DiscordMessage> GetResponseAsync(Func<DiscordMessage, bool> condition, int timeout = 30)
        {
            var pending = new PendingResponse(condition);
            _pendingResponses.TryAdd(pending, 0);

            try { await Task.Delay(timeout * 1000, pending.Token); }
            catch (OperationCanceledException) { }
            finally { _pendingResponses.TryRemove(pending); }

            return pending.Response;
        }




        private Task OnMessageReceived(DiscordClient client, MessageCreateEventArgs args)
        {
            var message = args.Message;
            if (message?.Author != null && !message.Author.IsBot
                    && message.Channel.BotCan(Permissions.SendMessages | Permissions.ReadMessageHistory))
            {
                try
                {
                    if (PendingResponse(message) ||
                        MessageGameInput(message) ||
                        Command(message, client))
                    {
                        if (message.Channel.Guild != null)
                        {
                            _ = EnsureUsersDownloadedAsync(message.Channel.Guild)
                                .LogExceptions(_log, $"Downloading users for guild {message.Channel.Guild.DebugName()}");
                        }
                    }
                }
                catch (Exception e)
                {
                    _log.Exception($"In {message.Channel.DebugName()}", e);
                }
            }

            return Task.CompletedTask;
        }

        private Task OnReactionAdded(DiscordClient client, MessageReactionAddEventArgs args)
            => OnReactionAddedOrRemoved(args.Message, args.User, args.Emoji);

        private Task OnReactionRemoved(DiscordClient client, MessageReactionRemoveEventArgs args)
            => OnReactionAddedOrRemoved(args.Message, args.User, args.Emoji);

        private Task OnReactionAddedOrRemoved(DiscordMessage message, DiscordUser user, DiscordEmoji emoji)
        {
            if (message.Channel.BotCan(Permissions.SendMessages | Permissions.ReadMessageHistory))
            {
                if (user.Id == _client.CurrentUser.Id) return Task.CompletedTask;
                
                var game = _games.AllGames
                    .OfType<IReactionsGame>()
                    .FirstOrDefault(g => g.MessageId == message.Id);

                if (game == null) return Task.CompletedTask;

                _ = ExecuteReactionGameInputAsync(game, message, user, emoji)
                    .LogExceptions(_log, $"During input \"{emoji.GetDiscordName()}\" in {message.Channel.DebugName()}");

                if (message.Channel?.Guild != null)
                {
                    _ = EnsureUsersDownloadedAsync(message.Channel.Guild)
                        .LogExceptions(_log, $"Downloading users for guild {message.Channel.Guild.DebugName()}");
                }
            }

            return Task.CompletedTask;
        }


        private Task OnCommandExecuted(CommandsNextExtension sender, CommandExecutionEventArgs args)
        {
            _log.Debug($"Executed {args.Command.Name} for {args.Context.User.DebugName()} in {args.Context.Channel.DebugName()}");
            return Task.CompletedTask;
        }


        private async Task OnCommandErrored(CommandsNextExtension sender, CommandErrorEventArgs args)
        {
            var ctx = args.Context;
            switch (args.Exception)
            {
                case ArgumentException e when e.Message.Contains("suitable overload"):
                    await ctx.RespondAsync($"Invalid command parameters for `{args.Command.Name}`");
                    return;

                case ChecksFailedException e:
                    switch (e.FailedChecks.First())
                    {
                        case RequireOwnerAttribute _:
                            return;

                        case RequireBotPermissionsAttribute r when ctx.Guild != null:
                            var curPerms = ctx.Channel.PermissionsFor(ctx.Guild.CurrentMember);
                            var perms = (r.Permissions ^ curPerms) & r.Permissions; // missing
                            await ctx.RespondAsync($"This bot requires the permission to {perms.ToPermissionString().ToLower()}!");
                            return;

                        case RequireUserPermissionsAttribute r when ctx.Guild != null:
                            curPerms = ctx.Channel.PermissionsFor(ctx.Member);
                            perms = (r.Permissions ^ curPerms) & r.Permissions; // missing
                            await ctx.RespondAsync($"You need the permission to {perms.ToPermissionString().ToLower()} to use this command.");
                            return;

                        case RequireDirectMessageAttribute _:
                        case RequireBotPermissionsAttribute _ when ctx.Guild == null:
                        case RequireUserPermissionsAttribute _ when ctx.Guild == null:
                            await ctx.RespondAsync("This command can only be used in DMs with the bot.");
                            return;

                        case RequireGuildAttribute _:
                            await ctx.RespondAsync("This command can only be used in a guild.");
                            return;

                        default:
                            await ctx.RespondAsync($"Can't execute command: {e.Message}");
                            return;
                    }

                case CommandNotFoundException e when args.Command.Name == "help":
                    await ctx.RespondAsync($"The command `{e.CommandName}` doesn't exist!");
                    return;

                case UnauthorizedException _ when args.Command.Name == "help":
                    await ctx.RespondAsync($"This bot requires the permission to use embeds!");
                    return;

                case UnauthorizedException e when args.Command.Name != "help":
                    await ctx.RespondAsync($"Something went wrong: The bot is missing permissions to perform this action!");
                    _log.Exception($"Bot is missing permissions in command {args.Command.Name}", e);
                    return;

                default:
                    _log.Exception($"While executing {args.Command?.Name} for {ctx.User.DebugName()} " +
                        $"in {ctx.Channel.DebugName()}", args.Exception);
                    try { await ctx.RespondAsync($"Something went wrong! {args.Exception.Message}"); }
                    catch (UnauthorizedException) { }
                    return;
            }
        }


        private async Task EnsureUsersDownloadedAsync(DiscordGuild guild)
        {
            if (guild != null && guild.MemberCount < 50000)
            {
                if (!_lastGuildUsersDownload.TryGetValue(guild.Id, out DateTime last)
                    || (DateTime.Now - last) > TimeSpan.FromMinutes(30))
                {
                    _lastGuildUsersDownload[guild.Id] = DateTime.Now;
                    await guild.RequestMembersAsync();
                    _log.Debug($"Downloaded users from {guild.DebugName()}");
                }
            }
        }


        /// <summary>Tries to find and complete a pending response. Returns whether it is successful.</summary>
        private bool PendingResponse(DiscordMessage message)
        {
            var pending = _pendingResponses.Select(x => x.Key).FirstOrDefault(x => x.Condition(message));

            if (pending != null)
            {
                pending.Response = message;
                return true;
            }

            return false;
        }


        /// <summary>Tries to find and execute a command. Returns whether it is successful.</summary>
        private bool Command(DiscordMessage message, DiscordClient client)
        {
            string prefix = _storage.GetGuildPrefix(message.Channel?.Guild);
            bool requiresPrefix = _storage.RequiresPrefix(message.Channel);

            int? selfMentionPos = message.GetMentionCommandPos(this);
            int pos = selfMentionPos
                ?? message.GetCommandPos(prefix)
                ?? (requiresPrefix ? -1 : 0);

            // I added a check for non-self mentions as the default prefix is < which is also the first character of discord mentions
            if (pos >= 0 && (selfMentionPos != null || !StartsWithAnyMention.IsMatch(message.Content)))
            {
                var commands = client.GetCommandsNext();
                var command = commands.FindCommand(message.Content.Substring(pos), out string rawArguments);
                if (command == null) return false;
                var context = commands.CreateContext(message, pos == 0 ? "" : prefix, command, rawArguments);
                _ = commands.ExecuteCommandAsync(context);
                return true;
            }

            return false;
        }


        /// <summary>Tries to find a game and execute message input. Returns whether it is successful.</summary>
        private bool MessageGameInput(DiscordMessage message)
        {
            var game = _games.GetForChannel<IMessagesGame>(message.Channel.Id);
            if (game == null || !game.IsInput(message.Content, message.Author.Id)) return false;

            _ = ExecuteMessageGameInputAsync(game, message)
                .LogExceptions(_log, $"During input \"{message.Content}\" in {game.Channel.DebugName()}");

            return true;
        }

        private async Task ExecuteMessageGameInputAsync(IMessagesGame game, DiscordMessage inputMsg)
        {
            _log.Debug($"Input {inputMsg.Content} by {inputMsg.Author.DebugName()} in {inputMsg.Channel.DebugName()}");

            var gameMessage = await game.GetMessageAsync();
            await game.InputAsync(inputMsg.Content, inputMsg.Author.Id);

            if (game is MultiplayerGame mGame)
            {
                while(await mGame.IsBotTurnAsync()) await mGame.BotInputAsync();
            }

            if (game.State != GameState.Active) _games.Remove(game);

            await game.UpdateMessageAsync(gameMessage, inputMsg);
        }


        private async Task ExecuteReactionGameInputAsync(IReactionsGame game, DiscordMessage message, DiscordUser user, DiscordEmoji emoji)
        {
            if (!await game.IsInputAsync(emoji, user.Id)) return;
            if (message == null) message = await game.GetMessageAsync();
            if (message == null) return; // oof

            var guild = message.Channel?.Guild;
            _log.Debug($"Input {emoji.GetDiscordName()} by {user.DebugName()} in {message.Channel?.DebugName()}");

            await game.InputAsync(emoji, user.Id);

            if (game.State != GameState.Active)
            {
                if (!(game is IUserGame)) _games.Remove(game);

                if (game is PacManGame pmGame && pmGame.State != GameState.Cancelled && !pmGame.custom)
                {
                    _storage.AddScore(new ScoreEntry(pmGame.score, user.Id, pmGame.State, pmGame.Time,
                        user.NameandDisc(), $"{guild?.Name}/{message.Channel?.Name}", DateTime.Now));
                }

                if (message.Channel.BotCan(Permissions.ManageMessages) && message != null)
                {
                    await message.DeleteAllReactionsAsync();
                }
            }

            await game.UpdateMessageAsync();
        }
    }
}
