using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Discord.Net;
using Discord.Commands;
using Discord.WebSocket;
using PacManBot.Games;
using PacManBot.Constants;
using static PacManBot.Games.GameUtils;

namespace PacManBot.Services
{
    public class InputService
    {
        private readonly DiscordShardedClient client;
        private readonly CommandService commands;
        private readonly StorageService storage;
        private readonly LoggingService logger;
        private readonly IServiceProvider provider;

        public readonly Regex waka = new Regex(@"^(w+a+k+a+\W*)+$", RegexOptions.IgnoreCase);


        public InputService(DiscordShardedClient client, CommandService commands, StorageService storage, LoggingService logger, IServiceProvider provider)
        {
            this.client = client;
            this.commands = commands;
            this.storage = storage;
            this.logger = logger;
            this.provider = provider;

            //Events
            client.MessageReceived += OnMessageReceived;
            client.ReactionAdded += OnReactionAdded;
            client.ReactionRemoved += OnReactionRemoved;
        }



        private Task OnMessageReceived(SocketMessage m)
        {
            _ = OnMessageReceivedAsync(m); // Discarding allows the async code to run without blocking the gateway task
            return Task.CompletedTask;
        }


        private Task OnReactionAdded(Cacheable<IUserMessage, ulong> m, ISocketMessageChannel c, SocketReaction r)
        {
            _ = OnReactionChangedAsync(m, c, r);
            return Task.CompletedTask;
        }


        private Task OnReactionRemoved(Cacheable<IUserMessage, ulong> m, ISocketMessageChannel c, SocketReaction r)
        {
            _ = OnReactionChangedAsync(m, c, r);
            return Task.CompletedTask;
        }




        private async Task OnMessageReceivedAsync(SocketMessage genericMessage)
        {
            try //I have to wrap discarded async methods in a try block so that exceptions don't go silent
            {
                if (client.CurrentUser != null && genericMessage is SocketUserMessage message
                    && !message.Author.IsBot && message.Channel.BotCan(ChannelPermission.SendMessages))
                {
                    // Only runs one
                    _ = await MessageGameInputAsync(message) || await CommandAsync(message) || await AutoresponseAsync(message);
                }
            }
            catch (Exception e)
            {
                await logger.Log(LogSeverity.Error, $"{e}");
            }
        }


        private async Task OnReactionChangedAsync(Cacheable<IUserMessage, ulong> messageData, ISocketMessageChannel channel, SocketReaction reaction)
        {
            try
            { //Maybe someone will one day make a bot that plays this bot
                if (client.CurrentUser != null && reaction.UserId != client.CurrentUser.Id)
                {
                    await ReactionGameInputAsync(messageData, channel, reaction);
                }
            }
            catch (Exception e)
            {
                await logger.Log(LogSeverity.Error, $"{e}");
            }
        }




        private async Task<bool> CommandAsync(SocketUserMessage message)
        {
            var context = new ShardedCommandContext(client, message);

            string prefix = storage.GetPrefix(context.Guild);
            int commandPosition = 0;
            
            if (message.HasMentionPrefix(client.CurrentUser, ref commandPosition) || message.HasStringPrefix($"{prefix} ", ref commandPosition) || message.HasStringPrefix(prefix, ref commandPosition)
                || context.Channel is IDMChannel || storage.NoPrefixChannels.Contains(message.Channel.Id))
            {
                var result = await commands.ExecuteAsync(context, commandPosition, provider);

                if (result.IsSuccess) return true;
                else if (!result.ErrorReason.Contains("Unknown command"))
                {
                    await logger.Log(LogSeverity.Verbose, $"Command {message} by {message.Author.FullName()} in channel {context.Channel.FullName()} couldn't be executed. {result.ErrorReason}");
                    string reply = GetCommandErrorReply(result.ErrorReason, context.Guild);
                    if (reply != null && context.BotCan(ChannelPermission.SendMessages))
                    {
                        await context.Channel.SendMessageAsync(reply, options: Utils.DefaultOptions);
                    }

                    return true;
                }
            }

            return false;
        }


        private async Task<bool> AutoresponseAsync(SocketUserMessage message)
        {
            if (!(message.Channel is SocketGuildChannel gChannel) || !storage.WakaExclude.Contains($"{gChannel.Guild.Id}") || Bot.AppInfo?.Owner.Id == message.Author.Id)
            {
                if (waka.IsMatch(message.Content))
                {
                    await message.Channel.SendMessageAsync("waka", options: Utils.DefaultOptions);
                    await logger.Log(LogSeverity.Verbose, $"Waka at {message.Channel.FullName()}");
                    return true;
                }
                else if (message.Content == "sudo neat")
                {
                    await message.Channel.SendMessageAsync("neat", options: Utils.DefaultOptions);
                    return true;
                }
            }

            return false;
        }


        private async Task<bool> MessageGameInputAsync(SocketUserMessage message)
        {
            var game = storage.GetGame<IMessagesGame>(message.Channel.Id);
            if (game == null || !game.IsInput(message.Content, message.Author.Id)) return false;

            try
            {
                await ExecuteGameInputAsync(game, message);
            }
            catch (Exception e) when (e is OperationCanceledException || e is TimeoutException) { }
            catch (HttpException e)
            {
                await logger.Log(LogSeverity.Warning, LogSource.Game, $"During {game.GetType().Name} input in {game.ChannelId}: {e.Message}");
            }

            return true;
        }


        private async Task<bool> ReactionGameInputAsync(Cacheable<IUserMessage, ulong> messageData, ISocketMessageChannel channel, SocketReaction reaction)
        {
            var game = storage.GetGame<IReactionsGame>(channel.Id);
            if (game == null || game.MessageId != reaction.MessageId || !game.IsInput(reaction.Emote, reaction.UserId)) return false;

            try
            {
                await ExecuteGameInputAsync(game, reaction, await messageData.GetOrDownloadAsync());
            }
            catch (Exception e) when (e is OperationCanceledException || e is TimeoutException) { }
            catch (HttpException e)
            {
                await logger.Log(LogSeverity.Warning, LogSource.Game, $"During Pac-Man input in {game.ChannelId}: {e.Message}");
            }

            return true;
        }




        private async Task ExecuteGameInputAsync(IMessagesGame game, IUserMessage message)
        {
            var gameMessage = await game.GetMessage();

            await logger.Log(LogSeverity.Verbose, game.Name, $"Input {message.Content} by user {message.Author.FullName()} on channel {message.Channel.FullName()}");

            game.DoInput(message.Content, message.Author.Id);
            if (game is MultiplayerGame mGame)
            {
                while(mGame.AITurn) mGame.DoTurnAI();
            }
            if (game.State != State.Active) storage.DeleteGame(game);

            game.CancelRequests();
            var requestOptions = game.RequestOptions;

            if (gameMessage != null && message.Channel.BotCan(ChannelPermission.ManageMessages))
            {
                await gameMessage.ModifyAsync(game.UpdateMessage, requestOptions);
                await message.DeleteAsync(Utils.DefaultOptions);
            }
            else
            {
                var newMsg = await gameMessage.Channel.SendMessageAsync(game.GetContent(), false, game.GetEmbed()?.Build(), requestOptions);
                game.MessageId = newMsg.Id;
                if (gameMessage != null) await gameMessage.DeleteAsync(Utils.DefaultOptions);
            }
        }


        private async Task ExecuteGameInputAsync(IReactionsGame game, SocketReaction reaction, IUserMessage gameMessage)
        {
            var user = reaction.User.IsSpecified ? reaction.User.Value : client.GetUser(reaction.UserId);
            var channel = gameMessage.Channel;
            var guild = (channel as IGuildChannel)?.Guild;

            await logger.Log(LogSeverity.Verbose, game.Name, $"Input {PacManGame.GameInputs[reaction.Emote].Align(5)} by user {user.FullName()} in channel {channel.FullName()}");

            game.DoTurn(reaction.Emote, user.Id);

            if (game.State != State.Active)
            {
                storage.DeleteGame(game);

                if (game is PacManGame pmGame && pmGame.State != State.Cancelled && !pmGame.custom)
                {
                    storage.AddScore(new ScoreEntry(pmGame.State, pmGame.score, pmGame.Time, user.Id, user.NameandNum(), DateTime.Now, $"{guild?.Name}/{channel.Name}"));
                }
                if (channel.BotCan(ChannelPermission.ManageMessages))
                {
                    await gameMessage.RemoveAllReactionsAsync(Utils.DefaultOptions);
                }
            }

            game.CancelRequests();
            await gameMessage.ModifyAsync(game.UpdateMessage, game.RequestOptions);
        }




        private string GetCommandErrorReply(string error, SocketGuild guild)
        {
            string help = $"Please use `{storage.GetPrefixOrEmpty(guild)}help [command name]` or try again.";

            if (error.Contains("Bot requires")) return guild == null ? "You need to be in a guild to use this command!"
                                                                     : $"This bot is missing the permission**{Regex.Replace(error.Split(' ').Last(), @"([A-Z])", @" $1")}**!";
            if (error.Contains("User requires")) return guild == null ? "You need to be in a guild to use this command!"
                                                                      : $"You need the permission**{Regex.Replace(error.Split(' ').Last(), @"([A-Z])", @" $1")}** to use this command!";
            if (error.Contains("User not found")) return $"Can't find the specified user!";
            if (error.Contains("Failed to parse")) return $"Invalid command parameters! {help}";
            if (error.Contains("too few parameters")) return $"Missing command parameters! {help}";
            if (error.Contains("too many parameters")) return $"Too many parameters! {help}";
            if (error.Contains("must be used in a guild")) return $"You need to be in a guild to use this command!";
            if (error.Contains("Timeout")) return $"You're using that command too much. Please try again later.";

            return null;
        }
    }
}
