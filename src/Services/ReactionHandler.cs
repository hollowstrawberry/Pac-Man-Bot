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


        private Task OnReactionAdded(Cacheable<IUserMessage, ulong> messageData, ISocketMessageChannel channel, SocketReaction reaction)
            => OnReaction(messageData, channel, reaction, false);


        private Task OnReactionRemoved(Cacheable<IUserMessage, ulong> messageData, ISocketMessageChannel channel, SocketReaction reaction)
            => OnReaction(messageData, channel, reaction, true);


        private Task OnReaction(Cacheable<IUserMessage, ulong> messageData, ISocketMessageChannel channel, SocketReaction reaction, bool removed)
        {
            Task.Run(async () => // Wrapping in a Task.Run prevents the gateway from getting blocked
            {
                if (!messageData.HasValue || !reaction.User.IsSpecified) return;

                ulong botID = client.CurrentUser.Id;
                if (messageData.Value.Author.Id == botID && reaction.UserId != botID)
                {
                    SocketCommandContext context = new SocketCommandContext(client, messageData.Value as SocketUserMessage);
                    await GameInput(context, reaction, removed);
                }
            });
            return Task.CompletedTask;
        }


        private async Task GameInput(SocketCommandContext context, SocketReaction reaction, bool removed)
        {
            if (removed && context.BotHas(ChannelPermission.ManageMessages)) return; //Removing reactions only counts if they're not automatically removed

            foreach (GameInstance game in storage.GameInstances)
            {
                if (context.Message.Id == game.messageId && game.state == GameInstance.State.Active) //Finds the game corresponding to this channel
                {
                    string emote = reaction.Emote.ToString();
                    var user = reaction.User.Value as SocketUser;
                    var message = context.Message;

                    if (GameInstance.GameInputs.ContainsKey(emote)) //Valid input
                    {
                        if (GameInstance.GameInputs.ContainsKey(emote)) //Valid reaction input
                        {
                            string strInput = GameInstance.GameInputs[emote].ToString();
                            await logger.Log(LogSeverity.Verbose, LogSource.Game, $"Input {strInput}{new string(' ', 5 - strInput.Length)} by user {user.FullName()} in channel {context.FullChannelName()}");

                            game.DoTick(GameInstance.GameInputs[emote]);

                            if (game.state != GameInstance.State.Active)
                            {
                                if (game.score > 0 && !game.custom)
                                {
                                    storage.AddScore(new ScoreEntry(game.state, game.score, game.time, user.Id, user.FullName(), DateTime.Now, (context.Guild != null ? $"{context.Guild.Name}/" : "") + context.Channel.Name));
                                }
                                storage.DeleteGame(game);
                            }

                            await message.ModifyAsync(m => m.Content = game.GetDisplay()); //Update display

                            if (game.state != GameInstance.State.Active) //Failsafe to bug where the display doesn't update in order if there are multiple inputs at once
                            {
                                await Task.Delay(3100);
                                await message.ModifyAsync(m => m.Content = game.GetDisplay());
                            }
                        }
                    }

                    if (context.BotHas(ChannelPermission.ManageMessages)) //Can remove reactions
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
}
