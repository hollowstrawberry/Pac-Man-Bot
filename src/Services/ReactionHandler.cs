using System;
using System.Threading.Tasks;
using Discord;
using Discord.Net;
using Discord.WebSocket;
using PacManBot.Constants;
using PacManBot.Modules.PacMan;
using static PacManBot.Modules.PacMan.GameInstance;

namespace PacManBot.Services
{
    class ReactionHandler
    {
        private readonly DiscordShardedClient client;
        private readonly StorageService storage;
        private readonly LoggingService logger;


        public ReactionHandler(DiscordShardedClient client, StorageService storage, LoggingService logger)
        {
            this.client = client;
            this.storage = storage;
            this.logger = logger;

            //Events
            client.ReactionAdded += OnReactionAdded;
            client.ReactionRemoved += OnReactionRemoved;
        }



        private Task OnReactionAdded(Cacheable<IUserMessage, ulong> m, ISocketMessageChannel c, SocketReaction r)
        {
            _ = OnReactionChangedAsync(m, c, r); // Discarding allows the async code to run without blocking the gateway task
            return Task.CompletedTask;
        }

        private Task OnReactionRemoved(Cacheable<IUserMessage, ulong> m, ISocketMessageChannel c, SocketReaction r)
        {
            _ = OnReactionChangedAsync(m, c, r);
            return Task.CompletedTask;
        }



        private async Task OnReactionChangedAsync(Cacheable<IUserMessage, ulong> m, ISocketMessageChannel c, SocketReaction r)
        {
            try //I have to wrap everything in a try so that exceptions don't go silent
            {
                await OnReactionChangedInternalAsync(m, c, r);
            }
            catch (Exception e)
            {
                await logger.Log(LogSeverity.Error, $"{e}");
            }
        }


        private async Task OnReactionChangedInternalAsync(Cacheable<IUserMessage, ulong> messageData, ISocketMessageChannel channel, SocketReaction reaction)
        {
            if (client.CurrentUser == null) return; // Not ready
            if (!reaction.User.IsSpecified || reaction.UserId == client.CurrentUser.Id) return;
 
            foreach (var game in storage.GameInstances) // Checks if the reacted message is a game
            {
                if (reaction.MessageId == game.messageId)
                {
                    var message = await messageData.GetOrDownloadAsync();
                    try
                    {
                        await PacManInput(game, message, reaction);
                    }
                    catch (TaskCanceledException) {}
                    catch (TimeoutException) {}
                    catch (HttpException e)
                    {
                        await logger.Log(LogSeverity.Warning, LogSource.Game, $"During game input in {game.channelId}: {e.Message}");
                    }

                    return;
                }
            }
        }


        private async Task PacManInput(GameInstance game, IUserMessage message, SocketReaction reaction)
        {
            var channel = message.Channel;
            var guild = (channel as IGuildChannel)?.Guild;

            if (game.state == State.Active)
            {
                IEmote emote = reaction.Emote;
                var user = reaction.User.Value as SocketUser;

                if (GameInputs.ContainsKey(emote)) //Valid reaction input
                {
                    await logger.Log(LogSeverity.Verbose, LogSource.Game + $"{(guild == null ? 0 : client.GetShardIdFor(guild))}",
                                     $"Input {GameInputs[emote].Align(5)} by user {user.FullName()} in channel {channel.FullName()}");

                    game.DoTurn(GameInputs[emote]);

                    if (game.state == State.Win || game.state == State.Lose)
                    {
                        if (!game.custom) storage.AddScore(
                            new ScoreEntry(game.state, game.score, game.time, user.Id, user.NameandNum(), DateTime.Now, $"{guild?.Name}/{channel.Name}")
                        );
                        storage.DeleteGame(game);
                    }

                    game.CancelPreviousEdits();
                    await message.ModifyAsync(m => m.Content = game.GetDisplay(), game.MessageEditOptions);
                }

                if (game.state != State.Active && channel.BotCan(ChannelPermission.ManageMessages))
                {
                    await message.RemoveAllReactionsAsync(Utils.DefaultRequestOptions);
                }
            }
        }
    }
}
