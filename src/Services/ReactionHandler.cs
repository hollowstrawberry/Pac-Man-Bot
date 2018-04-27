using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using PacManBot.Constants;
using PacManBot.Modules.PacMan;

namespace PacManBot.Services
{
    class ReactionHandler
    {
        private readonly DiscordSocketClient client;
        private readonly StorageService storage;
        private readonly LoggingService logger;


        public ReactionHandler(DiscordSocketClient client, StorageService storage, LoggingService logger)
        {
            this.client = client;
            this.storage = storage;
            this.logger = logger;

            // Events
            this.client.ReactionAdded += OnReactionAdded;
            this.client.ReactionRemoved += OnReactionRemoved;
        }


        private Task OnReactionAdded(Cacheable<IUserMessage, ulong> m, ISocketMessageChannel c, SocketReaction r)
        {
            Task.Run(async () => await OnReactionAsync(m, c, r, removed: false));
            return Task.CompletedTask;
        }

        private Task OnReactionRemoved(Cacheable<IUserMessage, ulong> m, ISocketMessageChannel c, SocketReaction r)
        {
            Task.Run(async () => await OnReactionAsync(m, c, r, removed: true));
            return Task.CompletedTask;
        }


        private async Task OnReactionAsync(Cacheable<IUserMessage, ulong> messageData, ISocketMessageChannel channel, SocketReaction reaction, bool removed)
        {
            if (!reaction.User.IsSpecified || reaction.UserId == client.CurrentUser.Id) return;

            foreach (GameInstance game in storage.GameInstances) // Checks if the reacted message is a game
            {
                if (reaction.MessageId == game.messageId)
                {
                    var message = await messageData.GetOrDownloadAsync();
                    if (message != null) await GameInput(game, message, reaction, removed);
                    else await logger.Log(LogSeverity.Warning, $"Couldn't grab game message {game.messageId} on channel {game.channelId}");
                    return;
                }
            }
        }


        private async Task GameInput(GameInstance game, IUserMessage message, SocketReaction reaction, bool removed)
        {
            var channel = message.Channel;
            var guild = (channel as IGuildChannel)?.Guild;

            if (removed && channel.BotHas(ChannelPermission.ManageMessages)) return; //Removing reactions only counts if they're not automatically removed

            if (game.state == GameInstance.State.Active)
            {
                IEmote emote = reaction.Emote;
                var user = reaction.User.Value as SocketUser;

                if (GameInstance.GameInputs.ContainsKey(emote)) //Valid reaction input
                {
                    string strInput = GameInstance.GameInputs[emote].ToString();
                    await logger.Log(LogSeverity.Verbose, LogSource.Game, $"Input {strInput}{new string(' ', 5 - strInput.Length)} by user {user.FullName()} in channel {guild?.Name}/{channel.Name} ({channel.Id})");

                    game.DoTick(GameInstance.GameInputs[emote]);

                    if (game.state != GameInstance.State.Active) // Ends game
                    {
                        if (!game.custom) storage.AddScore(
                            new ScoreEntry(game.state, game.score, game.time, user.Id, user.FullName(), DateTime.Now, $"{guild?.Name}/{channel.Name}")
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

                if (channel.BotHas(ChannelPermission.ManageMessages)) //Can remove reactions
                {
                    if (game.state == GameInstance.State.Active)
                    {
                        if (!removed) await message.RemoveReactionAsync(reaction.Emote, user);
                    }
                    else await message.RemoveAllReactionsAsync();
                }
            }
        }
    }
}
