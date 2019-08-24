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

        private readonly ulong[] bannedUsers;
        private readonly ulong[] bannedGuilds;

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

            bannedUsers = config.bannedUsers;
            bannedGuilds = config.bannedGuilds;

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
        public async Task<SocketUserMessage> GetResponseAsync(Func<SocketUserMessage, bool> condition, int timeout = 30)
        {
            var pending = new PendingResponse(condition);
            pendingResponses.TryAdd(pending, 0);

            try { await Task.Delay(timeout * 1000, pending.Token); }
            catch (OperationCanceledException) { }
            finally { pendingResponses.TryRemove(pending); }

            return pending.Response;
        }




        private Task OnMessageReceived(SocketMessage m)
        {
            HandleMessage(m); // Fire and forget
            return Task.CompletedTask;
        }


        private Task OnReactionAdded(Cacheable<IUserMessage, ulong> m, ISocketMessageChannel c, SocketReaction r)
        {
            HandleReaction(r, m, c);
            return Task.CompletedTask;
        }


        private Task OnReactionRemoved(Cacheable<IUserMessage, ulong> m, ISocketMessageChannel c, SocketReaction r)
        {
            HandleReaction(r, m, c);
            return Task.CompletedTask;
        }




        private async void HandleMessage(SocketMessage genericMessage)
        {
            try
            {
                if (bannedUsers.Contains(genericMessage.Author.Id)) return; // Spite in its purest form

                if (genericMessage.Channel is IGuildChannel gchannel && bannedGuilds.Contains(gchannel.GuildId))
                {
                    await gchannel.Guild.LeaveAsync();
                    return;
                }

                if (genericMessage is SocketUserMessage message && !message.Author.IsBot
                    && message.Channel.BotCan(ChannelPermission.SendMessages | ChannelPermission.ReadMessageHistory))
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


        private async void HandleReaction(SocketReaction reaction, Cacheable<IUserMessage, ulong> messageData, ISocketMessageChannel channel)
        {
            try
            {
                if (!channel.BotCan(ChannelPermission.SendMessages | ChannelPermission.ReadMessageHistory)) return;

                var message = reaction.Message.GetValueOrDefault() ?? await messageData.GetOrDownloadAsync();

                if (reaction.UserId != client.CurrentUser.Id && message?.Author.Id == client.CurrentUser.Id)
                {
                    await ReactionGameInputAsync(reaction, message, channel);
                }
            }
            catch (Exception e)
            {
                log.Exception($"In {channel.FullName()}", e);
            }
        }




        /// <summary>Tries to find and complete a pending response. Returns whether it is successful.</summary>
        private ValueTask<bool> PendingResponseAsync(SocketUserMessage message)
        {
            var pending = pendingResponses.Select(x => x.Key).FirstOrDefault(x => x.Condition(message));

            if (pending != null)
            {
                pending.Response = message;
                return new ValueTask<bool>(true);
            }

            return new ValueTask<bool>(false);
        }


        /// <summary>Tries to find and execute a command. Returns whether it is successful.</summary>
        private async ValueTask<bool> CommandAsync(SocketUserMessage message)
        {
            string prefix = await storage.GetGuildPrefixAsync((message.Channel as SocketGuildChannel)?.Guild);
            bool requiresPrefix = await storage.RequiresPrefixAsync(message.Channel);

            int pos = message.GetMentionCommandPos(client)
                ?? message.GetCommandPos(prefix)
                ?? (requiresPrefix ? -1 : 0);

            if (pos >= 0) await commands.ExecuteAsync(message, pos);

            return false;
        }


        /// <summary>Tries to find special messages to respond to. Returns whether it is successful.</summary>
        private async ValueTask<bool> AutoresponseAsync(SocketUserMessage message)
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
        private async ValueTask<bool> MessageGameInputAsync(SocketUserMessage message)
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
        private async ValueTask<bool> ReactionGameInputAsync(SocketReaction reaction, IUserMessage message, ISocketMessageChannel channel)
        {
            var game = games.AllGames
                .OfType<IReactionsGame>()
                .FirstOrDefault(g => g.MessageId == message.Id && g.IsInput(reaction.Emote, reaction.UserId));

            if (game == null) return false;

            try
            {
                await ExecuteGameInputAsync(game, reaction, message, channel);
            }
            catch (Exception e)
            {
                log.Exception($"During input \"{reaction.Emote.ReadableName()}\" in {channel.FullName()}", e, game.GameName);
            }

            return true;
        }




        private async Task ExecuteGameInputAsync(IMessagesGame game, SocketUserMessage message)
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


        private async Task ExecuteGameInputAsync(IReactionsGame game, SocketReaction reaction, IUserMessage message, ISocketMessageChannel channel)
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

                if (channel.BotCan(ChannelPermission.ManageMessages))
                {
                    await message.RemoveAllReactionsAsync(PmBot.DefaultOptions);
                }
            }

            game.CancelRequests();
            try { await message.ModifyAsync(game.GetMessageUpdate(), game.GetRequestOptions()); }
            catch (OperationCanceledException) { }
        }
    }
}
