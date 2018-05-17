using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Net;
using Discord.Commands;
using Discord.WebSocket;
using PacManBot.Games;
using PacManBot.Services;
using PacManBot.Constants;
using static PacManBot.Games.GameUtils;

namespace PacManBot.Modules
{
    [Name("👾More Games"), Remarks("2")]
    public class MoreGamesModule : ModuleBase<SocketCommandContext>
    {
        private readonly DiscordShardedClient shardedClient;
        private readonly LoggingService logger;
        private readonly StorageService storage;


        public MoreGamesModule(DiscordShardedClient shardedClient, LoggingService logger, StorageService storage)
        {
            this.shardedClient = shardedClient;
            this.logger = logger;
            this.storage = storage;
        }


        [Command("tictactoe"), Alias("ttt", "tic"), Remarks("Play Tic-Tac-Toe with another user or the bot")]
        [Summary("You can choose a guild member to invite as an opponent using a mention, username, nickname or user ID. Otherwise, you'll play against the bot.\n\n"
               + "You play by sending the number of a free cell (1 to 9) in chat while it is your turn, and to win you must make a line of 3 symbols in any direction\n\n"
               + "Do **{prefix}cancel** to end the game or **{prefix}bump** to move it to the bottom of the chat. The game times out after 5 minutes of inactivity.\n\n"
               + "You can also make the bot challenge another user or bot with **{prefix}ttt vs <opponent>**")]
        [RequireBotPermissionImproved(ChannelPermission.ReadMessageHistory | ChannelPermission.UseExternalEmojis | ChannelPermission.EmbedLinks)]
        public async Task StartTicTacToe(SocketGuildUser opponent = null)
        {
            await RunGame<TTTGame>(Context.User, opponent ?? (IUser)Context.Client.CurrentUser);
        }


        [Command("tictactoe vs"), Alias("ttt vs", "tic vs"), HideHelp]
        [Summary("Make the bot challenge a user... or another bot")]
        [RequireBotPermissionImproved(ChannelPermission.ReadMessageHistory | ChannelPermission.UseExternalEmojis | ChannelPermission.EmbedLinks)]
        public async Task StartTicTacToeVs(SocketGuildUser opponent)
        {
            await RunGame<TTTGame>(Context.Client.CurrentUser, opponent);
        }


        [Command("5ttt"), Alias("ttt5", "5tictactoe", "5tic"), Remarks("Play a harder 5-Tic-Tac-Toe with another user or the bot")]
        [Summary("You can choose a guild member to invite as an opponent using a mention, username, nickname or user ID. Otherwise, you'll play against the bot.\n\n"
               + "You play by sending the column and row of the cell you want to play, for example, \"C4\". The player who makes the **most lines of 3 symbols** wins. "
               + "However, if a player makes a lines of **4**, they win instantly.\n\n"
               + "Do **{prefix}cancel** to end the game or **{prefix}bump** to move it to the bottom of the chat. The game times out after 5 minutes of inactivity.\n\n"
               + "You can also make the bot challenge another user or bot with **{prefix}5ttt vs <opponent>**")]
        [RequireBotPermissionImproved(ChannelPermission.ReadMessageHistory | ChannelPermission.UseExternalEmojis | ChannelPermission.EmbedLinks)]
        public async Task StartTicTacToe5(SocketGuildUser opponent = null)
        {
            await RunGame<TTT5Game>(Context.User, opponent ?? (IUser)Context.Client.CurrentUser);
        }


        [Command("5ttt vs"), Alias("ttt5 vs", "5tictactoe vs", "5tic vs"), HideHelp]
        [Summary("Make the bot challenge a user... or another bot")]
        [RequireBotPermissionImproved(ChannelPermission.ReadMessageHistory | ChannelPermission.UseExternalEmojis | ChannelPermission.EmbedLinks)]
        public async Task Start5TicTacToeVs(SocketGuildUser opponent)
        {
            await RunGame<TTT5Game>(Context.Client.CurrentUser, opponent);
        }


        [Command("connect4"), Alias("c4", "four"), Remarks("Play Connect Four with another user or the bot")]
        [Summary("You can choose a guild member to invite as an opponent using a mention, username, nickname or user ID. Otherwise, you'll play against the bot.\n\n"
               + "You play by sending the number of a free cell (1 to 7) in chat while it is your turn, and to win you must make a line of 3 symbols in any direction\n\n"
               + "Do **{prefix}cancel** to end the game or **{prefix}bump** to move it to the bottom of the chat. The game times out after 5 minutes of inactivity.\n\n"
               + "You can also make the bot challenge another user or bot with **{prefix}c4 vs <opponent>**")]
        [RequireBotPermissionImproved(ChannelPermission.ReadMessageHistory | ChannelPermission.UseExternalEmojis | ChannelPermission.EmbedLinks)]
        public async Task StartConnectFour(SocketGuildUser opponent = null)
        {
            await RunGame<C4Game>(Context.User, opponent ?? (IUser)Context.Client.CurrentUser);
        }


        [Command("connect4 vs"), Alias("c4 vs", "four vs"), HideHelp]
        [Summary("Make the bot challenge a user... or another bot")]
        [RequireBotPermissionImproved(ChannelPermission.ReadMessageHistory | ChannelPermission.UseExternalEmojis | ChannelPermission.EmbedLinks)]
        public async Task StartConnectFoureVs(SocketGuildUser opponent)
        {
            await RunGame<C4Game>(Context.Client.CurrentUser, opponent);
        }


        private async Task RunGame<T>(IUser challenger, IUser opponent) where T : GameInstance
        {
            foreach (var otherGame in storage.GameInstances)
            {
                if (otherGame.channelId == Context.Channel.Id)
                {
                    await ReplyAsync(otherGame.userId.Contains(Context.User.Id) ?
                        $"You're already playing a game in this channel!\nUse **{storage.GetPrefixOrEmpty(Context.Guild)}cancel** if you want to cancel it." :
                        $"There is already a different game in this channel!\nWait until it's finished or try doing **{storage.GetPrefixOrEmpty(Context.Guild)}cancel**");
                    return;
                }
            }

            var players = new ulong[] { opponent.Id, challenger.Id };
            Type type = typeof(T);

            GameInstance game;
            if (type == typeof(TTTGame)) game = new TTTGame(Context.Channel.Id, players, shardedClient, logger, storage);
            else if (type == typeof(TTT5Game)) game = new TTT5Game(Context.Channel.Id, players, shardedClient, logger, storage);
            else if (type == typeof(C4Game)) game = new C4Game(Context.Channel.Id, players, shardedClient, logger, storage);
            else throw new NotImplementedException();

            bool bothBots = challenger.IsBot && opponent.IsBot;

            if (!bothBots && game.PlayingAI && !Context.BotCan(ChannelPermission.ManageMessages)) game.DoTurnAI();

            storage.AddGame(game);

            var gameMessage = await ReplyAsync(game.GetContent(), false, game.GetEmbed().Build(), Utils.DefaultRequestOptions);
            game.messageId = gameMessage.Id;

            while (game.state == State.Active)
            {
                if (game.PlayingAI && (Context.BotCan(ChannelPermission.ManageMessages) || bothBots))
                {
                    while (game.PlayingAI) // a loop for bot-on-bot matches
                    {
                        try
                        {
                            game.DoTurnAI();
                            if (game.messageId != gameMessage.Id) gameMessage = await game.GetMessage();
                            game.CancelRequests();
                            if (gameMessage != null) await gameMessage.ModifyAsync(m => { m.Content = game.GetContent(); m.Embed = game.GetEmbed()?.Build(); }, game.RequestOptions);
                        }
                        catch (Exception e) when (e is TaskCanceledException || e is TimeoutException || e is HttpException) { }

                        await Task.Delay(GlobalRandom.Next(500, 2001));
                    }
                }
                else await Task.Delay(1000);

                if ((DateTime.Now - game.lastPlayed) > game.Expiry)
                {
                    game.state = State.Cancelled;
                    storage.DeleteGame(game);
                    try
                    {
                        if (gameMessage.Id != game.messageId) gameMessage = await game.GetMessage();
                        if (gameMessage != null) await gameMessage.ModifyAsync(m => { m.Content = game.GetContent(); m.Embed = game.GetEmbed()?.Build(); }, Utils.DefaultRequestOptions);
                    }
                    catch (HttpException) { } // Something happened to the message, we can ignore it
                    return;
                }
            }

            if (storage.GameInstances.Contains(game)) storage.DeleteGame(game); // When playing against the bot
        }



        [Command("bump"), Alias("move", "refresh", "r")]
        [Remarks("Move any game to the bottom of the chat")]
        [Summary("Moves the current game's message in this channel to the bottom of the chat, deleting the old one."
               + "This is useful if the game got lost in a sea of other messages, or if the game stopped responding")]
        [RequireBotPermissionImproved(ChannelPermission.ReadMessageHistory | ChannelPermission.UseExternalEmojis | ChannelPermission.EmbedLinks | ChannelPermission.AddReactions)]
        private async Task MoveGame()
        {
            foreach (var game in storage.GameInstances)
            {
                if (game.channelId == Context.Channel.Id)
                {
                    try
                    {
                        var gameMessage = await game.GetMessage();
                        if (gameMessage != null) await gameMessage.DeleteAsync(Utils.DefaultRequestOptions); // Old message
                    }
                    catch (HttpException) { } // Something happened to the message, can ignore it

                    var message = await ReplyAsync(game.GetContent(), false, game.GetEmbed()?.Build(), Utils.DefaultRequestOptions);
                    game.messageId = message.Id;

                    if (game is PacManGame pacManGame) await PacManModule.AddControls(pacManGame, message);

                    return;
                }
            }

            await ReplyAsync("There is no active game in this channel!");
        }


        [Command("cancel"), Alias("end")]
        [Remarks("Cancel any game you're playing. Always usable by moderators")]
        [Summary("Cancels the current game in this channel, but only if you started or if nobody has played in over a minute. Always usable by users with the Manage Messages permission.")]
        [RequireBotPermissionImproved(ChannelPermission.ReadMessageHistory | ChannelPermission.UseExternalEmojis | ChannelPermission.EmbedLinks | ChannelPermission.AddReactions)]
        public async Task CancelGame()
        {
            foreach (var game in storage.GameInstances)
            {
                if (game.channelId == Context.Channel.Id)
                {
                    if (game.userId.Contains(Context.User.Id) || Context.UserCan(ChannelPermission.ManageMessages) || DateTime.Now - game.lastPlayed > TimeSpan.FromSeconds(60))
                    {
                        game.state = State.Cancelled;
                        storage.DeleteGame(game);

                        try
                        {
                            var gameMessage = await game.GetMessage();
                            if (gameMessage != null)
                            {
                                await gameMessage.ModifyAsync(m => { m.Content = game.GetContent(); m.Embed = game.GetEmbed()?.Build(); }, Utils.DefaultRequestOptions);
                                if (game is PacManGame && Context.BotCan(ChannelPermission.ManageMessages)) await gameMessage.RemoveAllReactionsAsync(Utils.DefaultRequestOptions);
                            }
                        }
                        catch (HttpException) { } // Something happened to the message, we can ignore it

                        if (game is PacManGame pacManGame && Context.Guild != null)
                        {
                            await ReplyAsync($"Game ended. Score won't be registered.\n**Result:** {pacManGame.score} points in {pacManGame.time} turns", options: Utils.DefaultRequestOptions);
                        }
                        else await Context.Message.AddReactionAsync(CustomEmoji.Check, Utils.DefaultRequestOptions);
                    }
                    else await ReplyAsync("You can't cancel this game because it's not yours and someone else is still playing!", options: Utils.DefaultRequestOptions);

                    return;
                }
            }

            await ReplyAsync("There is no active game in this channel!", options: Utils.DefaultRequestOptions);
        }
    }
}
