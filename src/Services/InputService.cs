using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
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
        private readonly DiscordShardedClient client;
        private readonly StorageService storage;
        private readonly LoggingService log;
        private readonly GameService games;

        private readonly ConcurrentDictionary<PendingResponse, byte> pendingResponses;
        private readonly ConcurrentDictionary<ulong, DateTime> lastGuildUsersDownload;

        private readonly Regex StartsWithAnyMention = new Regex(@"^<(@|#|a?:)");
        private Regex mentionPrefixRegex = null;

        /// <summary>Is a match when the given text begins with a mention to the bot's current user.</summary>
        public Regex MentionPrefix => mentionPrefixRegex ?? (mentionPrefixRegex = new Regex($@"^<@!?{client.CurrentUser.Id}>"));


        public InputService(DiscordShardedClient client, LoggingService log,
            StorageService storage, GameService games)
        {
            this.client = client;
            this.storage = storage;
            this.log = log;
            this.games = games;

            pendingResponses = new ConcurrentDictionary<PendingResponse, byte>();
            lastGuildUsersDownload = new ConcurrentDictionary<ulong, DateTime>();
        }


        /// <summary>Start listening to input events from Discord.</summary>
        public void StartListening(DiscordClient shard)
        {
            shard.MessageCreated += OnMessageReceived;
            shard.MessageReactionAdded += OnReactionAdded;
            shard.MessageReactionRemoved += OnReactionRemoved;
        }


        /// <summary>Stop listening to input events from Discord.</summary>
        public void StopListening(DiscordClient shard)
        {
            shard.MessageCreated -= OnMessageReceived;
            shard.MessageReactionAdded -= OnReactionAdded;
            shard.MessageReactionRemoved -= OnReactionRemoved;
        }


        /// <summary>Returns the first new message that satisfies the given condition within 
        /// a timeout period in seconds, or null if no match is received.</summary>
        public async Task<DiscordMessage> GetResponseAsync(Func<DiscordMessage, bool> condition, int timeout = 30)
        {
            var pending = new PendingResponse(condition);
            pendingResponses.TryAdd(pending, 0);

            try { await Task.Delay(timeout * 1000, pending.Token); }
            catch (OperationCanceledException) { }
            finally { pendingResponses.TryRemove(pending); }

            return pending.Response;
        }




        private Task OnMessageReceived(MessageCreateEventArgs args)
        {
            var message = args.Message;
            if (message.Author != null && !message.Author.IsBot
                    && message.Channel.BotCan(Permissions.SendMessages | Permissions.ReadMessageHistory))
            {
                _ = InnerOnMessageReceivedAsync(message, args.Client);
            }

            return Task.CompletedTask;
        }

        private async Task InnerOnMessageReceivedAsync(DiscordMessage message, DiscordClient client)
        {
            try
            {
                if (PendingResponse(message)
                    || await MessageGameInputAsync(message)
                    || await CommandAsync(message, client))
                {
                    if (message.Channel.Guild != null)
                    {
                        await EnsureUsersDownloadedAsync(message.Channel.Guild);
                    }
                }
            }
            catch (Exception e)
            {
                log.Exception($"In {message.Channel.DebugName()}", e);
            }
        }

        private Task OnReactionAdded(MessageReactionAddEventArgs args)
            => OnReactionAddedOrRemoved(args.Message, args.User, args.Emoji);

        private Task OnReactionRemoved(MessageReactionRemoveEventArgs args)
            => OnReactionAddedOrRemoved(args.Message, args.User, args.Emoji);

        private Task OnReactionAddedOrRemoved(DiscordMessage message, DiscordUser user, DiscordEmoji emoji)
        {
            if (message.Channel.BotCan(Permissions.SendMessages | Permissions.ReadMessageHistory))
            {
                if (user.Id == client.CurrentUser.Id) return Task.CompletedTask;
                
                // TODO: Can't check author ID in the current version of the library with message cache off
                //if (message != null && message.Author.Id != client.CurrentUser.Id) return Task.CompletedTask;

                var game = games.AllGames
                    .OfType<IReactionsGame>()
                    .FirstOrDefault(g => g.MessageId == message.Id);

                if (game == null) return Task.CompletedTask;

                _ = ExecuteReactionGameInputAsync(game, message, user, emoji);
                if (message.Channel?.Guild != null)
                {
                    _ = EnsureUsersDownloadedAsync(message.Channel.Guild);
                }
            }

            return Task.CompletedTask;
        }


        private async Task EnsureUsersDownloadedAsync(DiscordGuild guild)
        {
            if (guild != null && guild.MemberCount < 50000)
            {
                if (!lastGuildUsersDownload.TryGetValue(guild.Id, out DateTime last)
                    || (DateTime.Now - last) > TimeSpan.FromMinutes(30))
                {
                    lastGuildUsersDownload[guild.Id] = DateTime.Now;
                    int oldCount = guild.Members.Count();

                    await guild.RequestMembersAsync();

                    int time = (DateTime.Now - lastGuildUsersDownload[guild.Id]).Milliseconds;
                    log.Info($"Downloaded {guild.Members.Count() - oldCount} users from {guild.DebugName()} in {time}ms");
                }
            }
        }


        /// <summary>Tries to find and complete a pending response. Returns whether it is successful.</summary>
        private bool PendingResponse(DiscordMessage message)
        {
            var pending = pendingResponses.Select(x => x.Key).FirstOrDefault(x => x.Condition(message));

            if (pending != null)
            {
                pending.Response = message;
                return true;
            }

            return false;
        }


        /// <summary>Tries to find and execute a command. Returns whether it is successful.</summary>
        private async ValueTask<bool> CommandAsync(DiscordMessage message, DiscordClient client)
        {
            string prefix = storage.GetGuildPrefix(message.Channel?.Guild);
            bool requiresPrefix = storage.RequiresPrefix(message.Channel);

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
                await commands.ExecuteCommandAsync(context);
                return true;
            }

            return false;
        }


        /// <summary>Tries to find a game and execute message input. Returns whether it is successful.</summary>
        private async ValueTask<bool> MessageGameInputAsync(DiscordMessage message)
        {
            var game = games.GetForChannel<IMessagesGame>(message.Channel.Id);
            if (game == null || !await game.IsInputAsync(message.Content, message.Author.Id)) return false;

            try
            {
                await ExecuteMessageGameInputAsync(game, message);
            }
            catch (Exception e)
            {
                log.Exception($"During input \"{message.Content}\" in {game.Channel.DebugName()}", e, game.GameName);
            }

            return true;
        }

        private async Task ExecuteMessageGameInputAsync(IMessagesGame game, DiscordMessage message)
        {
            var gameMessage = await game.GetMessageAsync();

            log.Verbose(
                $"Input {message.Content} by {message.Author.DebugName()} in {message.Channel.DebugName()}",
                game.GameName);

            await game.InputAsync(message.Content, message.Author.Id);

            if (game is MultiplayerGame mGame)
            {
                while(await mGame.IsBotTurnAsync()) await mGame.BotInputAsync();
            }

            if (game.State != GameState.Active) games.Remove(game);

            if (gameMessage != null && message.Channel.BotCan(Permissions.ManageMessages))
            {
                await gameMessage.ModifyWithGameAsync(game);
                await message.DeleteAsync();
            }
            else
            {
                var newMsg = await message.Channel.SendMessageAsync(
                    await game.GetContentAsync(), false, (await game.GetEmbedAsync())?.Build());
                game.MessageId = newMsg.Id;

                if (gameMessage != null) await gameMessage.DeleteAsync();
            }
        }


        private async Task ExecuteReactionGameInputAsync(IReactionsGame game, DiscordMessage message, DiscordUser user, DiscordEmoji emoji)
        {
            try
            {
                if (!await game.IsInputAsync(emoji, user.Id)) return;
                if (message == null) message = await game.GetMessageAsync();
                if (message == null) return; // oof

                await InnerExecuteReactionGameInputAsync(game, message, user, emoji);
            }
            catch (Exception e)
            {
                log.Exception($"During input \"{emoji.GetDiscordName()}\" in {message.Channel.DebugName()}", e, game.GameName);
            }
        }

        private async Task InnerExecuteReactionGameInputAsync(IReactionsGame game, DiscordMessage message, DiscordUser user, DiscordEmoji emoji)
        {
            var guild = message.Channel?.Guild;
            log.Verbose(
                $"Input {emoji.GetDiscordName()} by {user.DebugName()} in {message.Channel?.DebugName()}",
                game.GameName);

            await game.InputAsync(emoji, user.Id);

            if (game.State != GameState.Active)
            {
                if (!(game is IUserGame)) games.Remove(game);

                if (game is PacManGame pmGame && pmGame.State != GameState.Cancelled && !pmGame.custom)
                {
                    storage.AddScore(new ScoreEntry(pmGame.score, user.Id, pmGame.State, pmGame.Time,
                        user.NameandDisc(), $"{guild?.Name}/{message.Channel?.Name}", DateTime.Now));
                }

                if (message.Channel.BotCan(Permissions.ManageMessages) && message != null)
                {
                    await message.DeleteAllReactionsAsync();
                }
            }

            if (message != null)
            {
                await message.ModifyWithGameAsync(game);
            }
        }
    }
}
