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
                if (client.CurrentUser == null // Not ready
                    || !(genericMessage is SocketUserMessage message)
                    || message.Author.IsBot || !message.Channel.BotCan(ChannelPermission.SendMessages)) return;

                // Only runs one
                _ = await CommandAsync(message) || await GameInputAsync(message) || await AutoresponseAsync(message);
            }
            catch (Exception e)
            {
                await logger.Log(LogSeverity.Error, $"{e}");
            }
        }


        private async Task OnReactionChangedAsync(Cacheable<IUserMessage, ulong> messageData, ISocketMessageChannel channel, SocketReaction reaction)
        {
            try
            {
                if (client.CurrentUser == null // Not ready
                    || !reaction.User.IsSpecified || reaction.UserId == client.CurrentUser.Id) return; //Maybe someone will one day makes a bot that plays this bot

                await PacManInputAsync(messageData, channel, reaction);
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
            
            if (message.HasMentionPrefix(client.CurrentUser, ref commandPosition) || message.HasStringPrefix($"{prefix} ", ref commandPosition) || message.HasStringPrefix(prefix, ref commandPosition) || context.Channel is IDMChannel)
            {
                var result = await commands.ExecuteAsync(context, commandPosition, provider);

                if (result.IsSuccess) return true;
                else if (!result.ErrorReason.Contains("Unknown command"))
                {
                    await logger.Log(LogSeverity.Verbose, $"Command {message} by {message.Author.FullName()} in channel {context.Channel.FullName()} couldn't be executed. {result.ErrorReason}");
                    string reply = GetCommandErrorReply(result.ErrorReason, context.Guild);
                    if (reply != null && context.BotCan(ChannelPermission.SendMessages))
                    {
                        await context.Channel.SendMessageAsync(reply, options: Utils.DefaultRequestOptions);
                    }

                    return true;
                }
            }

            return false;
        }


        private async Task<bool> AutoresponseAsync(SocketUserMessage message)
        {
            if (waka.IsMatch(message.ToString())
                && (!(message.Channel is SocketGuildChannel gChannel) || !storage.WakaExclude.Contains($"{gChannel.Guild.Id}")))
            {
                await message.Channel.SendMessageAsync("waka", options: Utils.DefaultRequestOptions);
                await logger.Log(LogSeverity.Verbose, $"Waka at {message.Channel.FullName()}");
                return true;
            }
            return false;
        }


        private async Task<bool> GameInputAsync(SocketUserMessage message)
        {
            foreach (var game in storage.GameInstances.Where(g => g is TTTGame || g is C4Game))
            {
                if (game.channelId == message.Channel.Id && game.userId[(int)game.turn] == message.Author.Id)
                {
                    string input = message.Content.Replace(storage.GetPrefix((message.Channel as IGuildChannel).Guild), "").Trim();
                    if (!game.GameInputs.ContainsKey(input)) return false;

                    try
                    {
                        await ExecuteGameInputAsync(game, message, game.GameInputs[input]);
                    }
                    catch (TaskCanceledException) { }
                    catch (TimeoutException) { }
                    catch (HttpException e)
                    {
                        await logger.Log(LogSeverity.Warning, LogSource.Game, $"During {game.GetType().Name} input in {game.channelId}: {e.Message}");
                    }

                    return true;
                }
            }
            return false;
        }


        private async Task<bool> PacManInputAsync(Cacheable<IUserMessage, ulong> messageData, ISocketMessageChannel channel, SocketReaction reaction)
        {
            foreach (PacManGame game in storage.GameInstances.Where(g => g is PacManGame))
            {
                if (game.messageId == reaction.MessageId && game.GameInputs.ContainsKey(reaction.Emote.ToString()))
                {
                    var message = await messageData.GetOrDownloadAsync();
                    try
                    {
                        await ExecutePacManInputAsync(game, message, reaction.User.Value, game.GameInputs[reaction.Emote.ToString()]);
                    }
                    catch (TaskCanceledException) { }
                    catch (TimeoutException) { }
                    catch (HttpException e)
                    {
                        await logger.Log(LogSeverity.Warning, LogSource.Game, $"During Pac-Man input in {game.channelId}: {e.Message}");
                    }

                    return true;
                }
            }

            return false;
        }




        private async Task ExecuteGameInputAsync(GameInstance game, IUserMessage message, GameInput input)
        {
            var gameMessage = await game.Channel.GetMessageAsync(game.messageId) as IUserMessage;

            game.DoTurn(input);

            if (game.winner != Player.None) storage.DeleteGame(game);

            if (message.Channel.BotCan(ChannelPermission.ManageMessages))
            {
                await gameMessage.ModifyAsync(m => { m.Embed = game.GetEmbed().Build(); m.Content = game.GetContent(); }, game.MessageEditOptions);
                await message.DeleteAsync(Utils.DefaultRequestOptions);
            }
            else
            {
                var newMsg = await gameMessage.Channel.SendMessageAsync(game.GetContent(), false, game.GetEmbed().Build(), game.MessageEditOptions);
                game.messageId = newMsg.Id;
                await gameMessage.DeleteAsync(Utils.DefaultRequestOptions);
            }
        }


        private async Task ExecutePacManInputAsync(PacManGame game, IUserMessage gameMessage, IUser user, GameInput input)
        {
            var channel = gameMessage.Channel;
            var guild = (channel as IGuildChannel)?.Guild;

            if (game.state == State.Active)
            {
                await logger.Log(LogSeverity.Verbose, LogSource.Game + $"{(guild == null ? 0 : client.GetShardIdFor(guild))}",
                                    $"Input {input.Align(5)} by user {user.FullName()} in channel {channel.FullName()}");

                game.DoTurn(input);

                if (game.state == State.Win || game.state == State.Lose)
                {
                    if (!game.custom) storage.AddScore(
                        new ScoreEntry(game.state, game.score, game.time, user.Id, user.NameandNum(), DateTime.Now, $"{guild?.Name}/{channel.Name}")
                    );
                    storage.DeleteGame(game);
                }

                game.CancelPreviousEdits();
                await gameMessage.ModifyAsync(m => m.Content = game.GetContent(), game.MessageEditOptions);

                if (game.state != State.Active && channel.BotCan(ChannelPermission.ManageMessages))
                {
                    await gameMessage.RemoveAllReactionsAsync(Utils.DefaultRequestOptions);
                }
            }
        }




        private string GetCommandErrorReply(string error, SocketGuild guild)
        {
            string help = $"Please use **{storage.GetPrefixOrEmpty(guild)}help [command name]** or try again.";

            if (error.Contains("Bot requires")) return guild == null ? "You need to be in a guild to use this command!"
                                                                     : $"This bot is missing the permission**{Regex.Replace(error.Split(' ').Last(), @"([A-Z])", @" $1")}**!";
            if (error.Contains("User requires")) return guild == null ? "You need to be in a guild to use this command!"
                                                                      : $"You need the permission**{Regex.Replace(error.Split(' ').Last(), @"([A-Z])", @" $1")}** to use this command!";
            if (error.Contains("User not found")) return $"Can't find the specified user!";
            if (error.Contains("Failed to parse")) return $"Invalid command parameters! {help}";
            if (error.Contains("too few parameters")) return $"Missing command parameters! {help}";
            if (error.Contains("too many parameters")) return $"Too many parameters! {help}";
            if (error.Contains("must be used in a guild")) return $"You need to be in a guild to use this command!";

            return null;
        }
    }
}
