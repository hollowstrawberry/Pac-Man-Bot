using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
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

        private readonly ulong[] bannedChannels;

        private static readonly Regex WakaRegex = new Regex(@"^(w+a+k+a+\W*)+$", RegexOptions.IgnoreCase);

        private readonly ConcurrentDictionary<PendingResponse, byte> pendingResponses;


        public InputService(PmConfig config, PmDiscordClient client, LoggingService log,
            StorageService storage, PmCommandService commands, GameService games)
        {
            this.client = client;
            this.commands = commands;
            this.storage = storage;
            this.log = log;
            this.games = games;

            bannedChannels = config.bannedChannels;

            pendingResponses = new ConcurrentDictionary<PendingResponse, byte>();
        }


        /// <summary>Start listening to input events from Discord.</summary>
        public void StartListening()
        {
            client.MessageReceived += OnMessageReceived;
            client.ReactionAdded += OnReactionAdded;
            client.ReactionRemoved += OnReactionRemoved;
        }


        /// <summary>Stop listening to input events from Discord.</summary>
        public void StopListening()
        {
            client.MessageReceived -= OnMessageReceived;
            client.ReactionAdded -= OnReactionAdded;
            client.ReactionRemoved -= OnReactionRemoved;
        }


        /// <summary>Returns the first new message that satisfies the given condition within 
        /// a timeout period in seconds, or null if no match is received.</summary>
        public async Task<SocketUserMessage> GetResponse(Func<SocketUserMessage, bool> condition, int timeout = 30)
        {
            var pending = new PendingResponse(condition);
            pendingResponses.TryAdd(pending, 0);

            try { await Task.Delay(timeout * 1000, pending.Token); }
            catch (OperationCanceledException) { }

            pendingResponses.TryRemove(pending);
            return pending.Response;
        }




        private Task OnMessageReceived(SocketMessage m)
        {
            OnMessageReceivedAsync(m); // Fire and forget
            return Task.CompletedTask;
        }


        private Task OnReactionAdded(Cacheable<IUserMessage, ulong> m, ISocketMessageChannel c, SocketReaction r)
        {
            OnReactionChangedAsync(m, c, r);
            return Task.CompletedTask;
        }


        private Task OnReactionRemoved(Cacheable<IUserMessage, ulong> m, ISocketMessageChannel c, SocketReaction r)
        {
            OnReactionChangedAsync(m, c, r);
            return Task.CompletedTask;
        }




        private async void OnMessageReceivedAsync(SocketMessage genericMessage)
        {
            try
            {
                if (bannedChannels.Contains(genericMessage.Channel.Id))
                {
                    if (genericMessage.Channel is IGuildChannel guildChannel) await guildChannel.Guild.LeaveAsync();
                    return;
                }

                if (genericMessage is SocketUserMessage message && !message.Author.IsBot
                    && await message.Channel.BotCan(ChannelPermission.SendMessages))
                {
                    // Short-circuits on the first accepted case
                    if (   await PendingResponseAsync(message)
                        || await MessageGameInputAsync(message)
                        || await CommandAsync(message)
                        || await AutoresponseAsync(message)
                    ) { }
                }
            }
            catch (Exception e)
            {
                log.Exception($"In {genericMessage.Channel.FullName()}", e);
            }
        }


        private async void OnReactionChangedAsync(Cacheable<IUserMessage, ulong> messageData, ISocketMessageChannel channel, SocketReaction reaction)
        {
            try
            {
                if (!await channel.BotCan(ChannelPermission.ReadMessageHistory)) return;

                var message = reaction.Message.Value ?? await messageData.GetOrDownloadAsync();

                if (reaction.UserId != client.CurrentUser.Id && message?.Author.Id == client.CurrentUser.Id)
                {
                    await ReactionGameInputAsync(message, channel, reaction);
                }
            }
            catch (Exception e)
            {
                log.Exception($"In {channel.FullName()}", e);
            }
        }




        /// <summary>Tries to find and complete a pending response. Returns whether it is successful.</summary>
        private Task<bool> PendingResponseAsync(SocketUserMessage message)
        {
            var pending = pendingResponses.Select(x => x.Key).FirstOrDefault(x => x.Condition(message));

            if (pending != null)
            {
                pending.Response = message;
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }


        /// <summary>Tries to find and execute a command. Returns whether it is successful.</summary>
        private async Task<bool> CommandAsync(SocketUserMessage message)
        {
            string prefix = await storage.GetGuildPrefixAsync((message.Channel as SocketGuildChannel)?.Guild);
            int commandPosition = 0;

            if (message.HasMentionPrefix(client.CurrentUser, ref commandPosition)
                || message.HasStringPrefix($"{prefix} ", ref commandPosition)
                || message.HasStringPrefix(prefix, ref commandPosition)
                || !await storage.RequiresPrefixAsync(message.Channel))
            {
                await commands.ExecuteAsync(message, commandPosition);
            }

            return false;
        }


        /// <summary>Tries to find special messages to respond to. Returns whether it is successful.</summary>
        private async Task<bool> AutoresponseAsync(SocketUserMessage message)
        {
            if (!(message.Channel is SocketGuildChannel gChannel) || await storage.AllowsAutoresponseAsync(gChannel.Guild))
            {
                if (WakaRegex.IsMatch(message.Content))
                {
                    await message.Channel.SendMessageAsync("waka", options: PmBot.DefaultOptions);
                    log.Verbose($"Waka at {message.Channel.FullName()}");
                    return true;
                }
                else if (message.Content == "sudo neat")
                {
                    await message.Channel.SendMessageAsync("neat", options: PmBot.DefaultOptions);
                    return true;
                }
            }

            return false;
        }


        /// <summary>Tries to find a game and execute message input. Returns whether it is successful.</summary>
        private async Task<bool> MessageGameInputAsync(SocketUserMessage message)
        {
            var game = games.GetForChannel<IMessagesGame>(message.Channel.Id);
            if (game == null || !game.IsInput(message.Content, message.Author.Id)) return false;

            try
            {
                await ExecuteGameInputAsync(game, message);
            }
            catch (Exception e)
            {
                log.Exception($"During input \"{message.Content}\" in {game.Channel.FullName()}", e, game.GameName);
            }

            return true;
        }


        /// <summary>Tries to find a game and execute reaction input. Returns whether it is successful.</summary>
        private async Task<bool> ReactionGameInputAsync(IUserMessage message, ISocketMessageChannel channel, SocketReaction reaction)
        {
            var game = games.AllGames
                .OfType<IReactionsGame>()
                .FirstOrDefault(g => g.MessageId == message.Id && g.IsInput(reaction.Emote, reaction.UserId));

            if (game == null) return false;

            try
            {
                await ExecuteGameInputAsync(game, reaction, message);
            }
            catch (Exception e)
            {
                log.Exception($"During input \"{reaction.Emote.ReadableName()}\" in {channel.FullName()}", e, game.GameName);
            }

            return true;
        }




        private async Task ExecuteGameInputAsync(IMessagesGame game, IUserMessage message)
        {
            var gameMessage = await game.GetMessage();

            log.Verbose(
                $"Input {message.Content} by {message.Author.FullName()} in {message.Channel.FullName()}",
                game.GameName);

            game.Input(message.Content, message.Author.Id);
            if (game is MultiplayerGame mGame)
            {
                while(mGame.BotTurn) mGame.BotInput();
            }
            if (game.State != State.Active) games.Remove(game);

            if (gameMessage != null && await message.Channel.BotCan(ChannelPermission.ManageMessages))
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


        private async Task ExecuteGameInputAsync(IReactionsGame game, SocketReaction reaction, IUserMessage gameMessage)
        {
            var user = reaction.User.IsSpecified ? reaction.User.Value : client.GetUser(reaction.UserId);
            var channel = gameMessage.Channel;
            var guild = (channel as IGuildChannel)?.Guild;

            log.Verbose(
                $"Input {reaction.Emote.ReadableName()} by {user.FullName()} in {channel.FullName()}",
                game.GameName);

            game.Input(reaction.Emote, user.Id);

            if (game.State != State.Active)
            {
                if (!(game is IUserGame)) games.Remove(game);

                if (game is PacManGame pmGame && pmGame.State != State.Cancelled && !pmGame.custom)
                {
                    storage.AddScore(new ScoreEntry(pmGame.score, user.Id, pmGame.State, pmGame.Time,
                        user.NameandDisc(), $"{guild?.Name}/{channel.Name}", DateTime.Now));
                }

                if (await channel.BotCan(ChannelPermission.ManageMessages))
                {
                    await gameMessage.RemoveAllReactionsAsync(PmBot.DefaultOptions);
                }
            }

            game.CancelRequests();
            try { await gameMessage.ModifyAsync(game.GetMessageUpdate(), game.GetRequestOptions()); }
            catch (OperationCanceledException) { }
        }
    }
}
