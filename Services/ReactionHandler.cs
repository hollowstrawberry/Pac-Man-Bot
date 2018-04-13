using System;
using System.IO;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using PacManBot.Constants;
using static PacManBot.Modules.PacMan.PacManGame;

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

            this.client.ReactionAdded += OnReactionAdded; //Events
            this.client.ReactionRemoved += OnReactionRemoved;
        }


        private Task OnReactionAdded(Cacheable<IUserMessage, ulong> messageData, ISocketMessageChannel channel, SocketReaction reaction)
            => OnReaction(messageData, channel, reaction, false);


        private Task OnReactionRemoved(Cacheable<IUserMessage, ulong> messageData, ISocketMessageChannel channel, SocketReaction reaction)
            => OnReaction(messageData, channel, reaction, true);


        private Task OnReaction(Cacheable<IUserMessage, ulong> messageData, ISocketMessageChannel channel, SocketReaction reaction, bool removed)
        {
            Task.Run(async () => //Wrapping in a Task.Run prevents the gateway from getting blocked in case something goes wrong
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

            foreach (Modules.PacMan.PacManGame game in storage.gameInstances)
            {
                if (context.Message.Id == game.messageId && game.state == State.Active) //Finds the game corresponding to this channel
                {
                    string emote = reaction.Emote.ToString();
                    string channelName = context.FullChannelName();
                    var user = reaction.User.Value as SocketUser;
                    var message = context.Message;

                    if (gameInput.ContainsKey(emote)) //Valid input
                    {
                        if (gameInput.ContainsKey(emote)) //Valid reaction input
                        {
                            await logger.Log(LogSeverity.Verbose, "Game", $"Input \"{gameInput[emote]}\" by user {user.FullName()} in channel {channelName}");
                            game.DoTick(gameInput[emote]);

                            if (game.state != State.Active)
                            {
                                storage.gameInstances.Remove(game);
                                if (game.score > 0 && !game.custom)
                                {
                                    File.AppendAllText(BotFile.Scoreboard, $"\n{game.state} {game.score} {game.time} {user.Id} \"{user.Username}#{user.Discriminator}\" \"{DateTime.Now.ToString("o")}\" \"{channelName}\"");
                                    await logger.Log(LogSeverity.Verbose, "Game", $"({game.state}) Achieved score {game.score} in {game.time} moves in channel {channelName} last controlled by user {user.FullName()}");
                                }
                            }

                            await message.ModifyAsync(m => m.Content = game.GetDisplay()); //Update display

                            if (game.state != State.Active) //Failsafe to bug where the display doesn't update in order if there are multiple inputs at once
                            {
                                await Task.Delay(3100);
                                await message.ModifyAsync(m => m.Content = game.GetDisplay());
                            }
                        }
                    }

                    if (context.BotHas(ChannelPermission.ManageMessages)) //Can remove reactions
                    {
                        if (game.state == State.Active)
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
