using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
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
        private readonly PmDiscordClient client;
        private readonly PmCommandService commands;
        private readonly StorageService storage;
        private readonly LoggingService log;
        private readonly GameService games;

        private readonly ConcurrentDictionary<PendingResponse, byte> pendingResponses;


        public InputService(PmConfig config, PmDiscordClient client, LoggingService log,
            StorageService storage, PmCommandService commands, GameService games)
        {
            this.client = client;
            this.commands = commands;
            this.storage = storage;
            this.log = log;
            this.games = games;

            pendingResponses = new ConcurrentDictionary<PendingResponse, byte>();
        }


        /// <summary>Start listening to input events from Discord.</summary>
        public void StartListening()
        {
            client.MessageReceived += OnMessageReceived;
            client.ReactionAdded += OnReactionAddedOrRemoved;
            client.ReactionRemoved += OnReactionAddedOrRemoved;
        }


        /// <summary>Stop listening to input events from Discord.</summary>
        public void StopListening()
        {
            client.MessageReceived -= OnMessageReceived;
            client.ReactionAdded -= OnReactionAddedOrRemoved;
            client.ReactionRemoved -= OnReactionAddedOrRemoved;
        }


        /// <summary>Returns the first new message that satisfies the given condition within 
        /// a timeout period in seconds, or null if no match is received.</summary>
        public async Task<SocketUserMessage> GetResponseAsync(Func<SocketUserMessage, bool> condition, int timeout = 30)
        {
            var pending = new PendingResponse(condition);
            pendingResponses.TryAdd(pending, 0);

            try { await Task.Delay(timeout * 1000, pending.Token); }
            catch (OperationCanceledException) { }
            finally { pendingResponses.TryRemove(pending); }

            return pending.Response;
        }




        private Task OnMessageReceived(SocketMessage genericMessage)
        {
            if (genericMessage is SocketUserMessage message && !message.Author.IsBot
                    && message.Channel.BotCan(ChannelPermission.SendMessages | ChannelPermission.ReadMessageHistory))
            {
                try
                {
                    if (PendingResponse(message) || MessageGameInput(message) || Command(message))
                    {
                        return Task.CompletedTask;
                    }
                }
                catch (Exception e)
                {
                    log.Exception($"In {message.Channel.FullName()}", e);
                }
            }

            return Task.CompletedTask;
        }


        private Task OnReactionAddedOrRemoved(Cacheable<IUserMessage, ulong> messageData, ISocketMessageChannel channel, SocketReaction reaction)
        {
            if (channel.BotCan(ChannelPermission.SendMessages | ChannelPermission.ReadMessageHistory))
            {
                if (reaction.UserId == client.CurrentUser.Id) return Task.CompletedTask;

                IUserMessage message = reaction.Message.GetValueOrDefault();
                if (message == null && messageData.HasValue) message = messageData.Value;

                if (message != null && message.Author.Id != client.CurrentUser.Id) return Task.CompletedTask;

                var game = games.AllGames
                    .OfType<IReactionsGame>()
                    .FirstOrDefault(g => g.MessageId == reaction.MessageId);

                if (game == null || !game.IsInput(reaction.Emote, reaction.UserId)) return Task.CompletedTask;

                _ = ExecuteReactionGameInputAsync(game, reaction, message, channel);
            }

            return Task.CompletedTask;
        }


        /// <summary>Tries to find and complete a pending response. Returns whether it is successful.</summary>
        private bool PendingResponse(SocketUserMessage message)
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
        private bool Command(SocketUserMessage message)
        {
            string prefix = storage.GetGuildPrefix((message.Channel as SocketGuildChannel)?.Guild);
            bool requiresPrefix = storage.RequiresPrefix(message.Channel);

            int pos = message.GetMentionCommandPos(client)
                ?? message.GetCommandPos(prefix)
                ?? (requiresPrefix ? -1 : 0);

            if (pos >= 0)
            {
                _ = commands.ExecuteAsync(message, pos);
                return true;
            }

            return false;
        }


        /// <summary>Tries to find a game and execute message input. Returns whether it is successful.</summary>
        private bool MessageGameInput(SocketUserMessage message)
        {
            var game = games.GetForChannel<IMessagesGame>(message.Channel.Id);
            if (game == null || !game.IsInput(message.Content, message.Author.Id)) return false;

            _ = ExecuteGameInputAsync(game, message);

            return true;
        }

        private async Task ExecuteGameInputAsync(IMessagesGame game, SocketUserMessage message)
        {
            try
            {
                await InnerExecuteGameInputAsync(game, message);
            }
            catch (Exception e)
            {
                log.Exception($"During input \"{message.Content}\" in {game.Channel.FullName()}", e, game.GameName);
            }
        }

        private async Task InnerExecuteGameInputAsync(IMessagesGame game, SocketUserMessage message)
        {
            var gameMessage = await game.GetMessageAsync();

            log.Verbose(
                $"Input {message.Content} by {message.Author.FullName()} in {message.Channel.FullName()}",
                game.GameName);

            await game.InputAsync(message.Content, message.Author.Id);

            if (game is MultiplayerGame mGame)
            {
                while(mGame.BotTurn) await mGame.BotInputAsync();
            }

            if (game.State != GameState.Active) games.Remove(game);

            if (gameMessage != null && message.Channel.BotCan(ChannelPermission.ManageMessages))
            {
                game.CancelRequests();
                try { await gameMessage.ModifyAsync(game.GetMessageUpdate(), game.GetRequestOptions()); }
                catch (OperationCanceledException) { }

                await message.DeleteAsync(PmBot.DefaultOptions);
            }
            else
            {
                game.CancelRequests();
                try
                {
                    var newMsg = await message.Channel.SendMessageAsync(game.GetContent(), false, game.GetEmbed()?.Build(), game.GetRequestOptions());
                    game.MessageId = newMsg.Id;
                }
                catch (OperationCanceledException) { }

                if (gameMessage != null) await gameMessage.DeleteAsync(PmBot.DefaultOptions);
            }
        }


        private async Task ExecuteReactionGameInputAsync(IReactionsGame game, SocketReaction reaction, IUserMessage message, ISocketMessageChannel channel)
        {
            try
            {
                if (message == null) message = await game.GetMessageAsync();
                if (message == null) return; // oof

                await InnerExecuteReactionGameInputAsync(game, reaction, message, channel);
            }
            catch (Exception e)
            {
                log.Exception($"During input \"{reaction.Emote.ReadableName()}\" in {channel.FullName()}", e, game.GameName);
            }
        }

        private async Task InnerExecuteReactionGameInputAsync(IReactionsGame game, SocketReaction reaction, IUserMessage message, ISocketMessageChannel channel)
        {
            var userId = reaction.UserId;
            var user = reaction.User.GetValueOrDefault() ?? client.GetUser(reaction.UserId);
            var guild = (channel as SocketTextChannel)?.Guild;

            log.Verbose(
                $"Input {reaction.Emote.ReadableName()} by {user?.FullName()} in {channel.FullName()}",
                game.GameName);

            await game.InputAsync(reaction.Emote, userId);

            if (game.State != GameState.Active)
            {
                if (!(game is IUserGame)) games.Remove(game);

                if (game is PacManGame pmGame && pmGame.State != GameState.Cancelled && !pmGame.custom)
                {
                    storage.AddScore(new ScoreEntry(pmGame.score, userId, pmGame.State, pmGame.Time,
                        user?.NameandDisc(), $"{guild?.Name}/{channel.Name}", DateTime.Now));
                }

                if (channel.BotCan(ChannelPermission.ManageMessages) && message != null)
                {
                    await message.RemoveAllReactionsAsync(PmBot.DefaultOptions);
                }
            }

            game.CancelRequests();
            if (message != null)
            {
                try { await message.ModifyAsync(game.GetMessageUpdate(), game.GetRequestOptions()); }
                catch (OperationCanceledException) { }
            }
        }
    }
}
