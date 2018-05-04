using System;
using System.Threading.Tasks;
using Discord;
using Discord.Net;
using Discord.WebSocket;
using PacManBot.Constants;
using PacManBot.Modules.PacMan;

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



        private async Task OnReactionChangedAsync(Cacheable<IUserMessage, ulong> m, ISocketMessageChannel c, SocketReaction r) //I have to do this so that exceptions don't go silent
        {
            try
            {
                await OnReactionChangedInternal(m, c, r);
            }
            catch (Exception e)
            {
                await logger.Log(LogSeverity.Error, $"{e}");
            }
        }


        private async Task OnReactionChangedInternal(Cacheable<IUserMessage, ulong> messageData, ISocketMessageChannel channel, SocketReaction reaction)
        {
            if (client.CurrentUser == null) return; // Not ready
            if (!reaction.User.IsSpecified || reaction.User.Value.IsBot) return;

            foreach (GameInstance game in storage.GameInstances) // Checks if the reacted message is a game
            {
                if (reaction.MessageId == game.messageId)
                {
                    var message = await messageData.GetOrDownloadAsync();
                    try
                    {
                        await PacManInput(game, message, reaction);
                    }
                    catch (RateLimitedException)
                    {
                        await logger.Log(LogSeverity.Warning, LogSource.Game, $"Rate limit during input in {game.channelId}");
                    }
                    catch (Exception e) when (e is HttpException || e is TimeoutException || e is TaskCanceledException)
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

            if (game.state == GameInstance.State.Active)
            {
                IEmote emote = reaction.Emote;
                var user = reaction.User.Value as SocketUser;

                if (GameInstance.GameInputs.ContainsKey(emote)) //Valid reaction input
                {
                    await logger.Log(LogSeverity.Verbose, LogSource.Game + $"{(guild == null ? 0 : client.GetShardIdFor(guild))}",
                                     $"Input {GameInstance.GameInputs[emote].Align(5)} by user {user.FullName()} in channel {channel.FullName()}");

                    game.DoTick(GameInstance.GameInputs[emote]);

                    if (game.state != GameInstance.State.Active) // Ends game
                    {
                        if (!game.custom) storage.AddScore(
                            new ScoreEntry(game.state, game.score, game.time, user.Id, user.NameandNum(), DateTime.Now, $"{guild?.Name}/{channel.Name}")
                        );
                        storage.DeleteGame(game);
                    }

                    await message.ModifyAsync(m => m.Content = game.GetDisplay()); //Update display

                    if (game.state != GameInstance.State.Active) //Failsafe to bug where the display doesn't update in order if there are multiple inputs at once
                    {
                        await Task.Delay(3100);
                        await message.ModifyAsync(m => m.Content = game.GetDisplay());
                    }
                }

                if (game.state != GameInstance.State.Active && channel.BotCan(ChannelPermission.ManageMessages))
                {
                    await message.RemoveAllReactionsAsync();
                }
            }
        }
    }
}
