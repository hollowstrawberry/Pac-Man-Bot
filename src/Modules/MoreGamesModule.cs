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
        [Summary("You can choose a guild member to invite as an opponent using a mention, username, nickname or user ID. Otherwise, you'll play against the bot.\n"
               + "The game is played by sending the number of a free cell (1 to 9) in chat while it is your turn. Using a prefix is unnecessary when sending a number.\n\n"
               + "You can cancel the game at any time by using **{prefix}cancel**, or move it to the bottom of the chat by using **{prefix}move**.\n"
               + "The game times out if 2 minutes pass without any input.")]
        public async Task StartTicTacToe(SocketGuildUser opponent = null) => await RunGame<TTTGame>(opponent?.Id ?? Context.Client.CurrentUser.Id);


        [Command("connect4"), Alias("c4", "four"), Remarks("Play Connect Four with another user or the bot")]
        [Summary("You can choose a guild member to invite as an opponent using a mention, username, nickname or user ID. Otherwise, you'll play against the bot.\n"
               + "The game is played by sending the number of a column (1 to 7) in chat while it is your turn. Using a prefix is unnecessary when sending a number.\n\n"
               + "You can cancel the game at any time by using **{prefix}cancel**, or move it to the bottom of the chat by using **{prefix}move**.\n"
               + "The game times out if 2 minutes pass without any input.")]
        public async Task StartConnectFour(SocketGuildUser opponent = null) => await RunGame<C4Game>(opponent?.Id ?? Context.Client.CurrentUser.Id);


        private async Task RunGame<T>(ulong opponentId) where T : GameInstance
        {
            foreach (var game in storage.GameInstances)
            {
                if (game.channelId == Context.Channel.Id)
                {
                    await ReplyAsync(game.userId.Contains(Context.User.Id) ?
                        $"You're already playing a game in this channel!\nUse **{storage.GetPrefixOrEmpty(Context.Guild)}cancel** if you want to cancel it." :
                        "There is already a different game in this channel!\nWait until it's finished, it times out, or a moderator cancels it.");
                    return;
                }
            }

            var players = new ulong[] { opponentId, Context.User.Id };
            Type type = typeof(T);

            GameInstance newGame;
            if (type == typeof(TTTGame)) newGame = new TTTGame(Context.Channel.Id, players, shardedClient, logger, storage);
            else if (type == typeof(C4Game)) newGame = new C4Game(Context.Channel.Id, players, shardedClient, logger, storage);
            else throw new NotImplementedException();

            storage.AddGame(newGame);

            var gameMessage = await ReplyAsync(newGame.GetContent(), false, newGame.GetEmbed().Build(), Utils.DefaultRequestOptions);
            newGame.messageId = gameMessage.Id;

            while (newGame.state == State.Active)
            {
                if ((DateTime.Now - newGame.lastPlayed) > newGame.Expiry)
                {
                    newGame.state = State.Cancelled;
                    storage.DeleteGame(newGame);
                    try
                    {
                        if (gameMessage.Id != newGame.messageId) gameMessage = await newGame.GetMessage();
                        if (gameMessage != null) await gameMessage.ModifyAsync(m => { m.Content = newGame.GetContent(); m.Embed = newGame.GetEmbed()?.Build(); }, Utils.DefaultRequestOptions);
                    }
                    catch (HttpException) { } // Something happened to the message, we can ignore it
                    return;
                }
                await Task.Delay(1000);
            }
        }



        [Command("move"), Alias("refresh", "r")]
        [Remarks("Move any game to the bottom of the chat")]
        [Summary("Moves the current game's message in this channel to the bottom of the chat, deleting the old one."
               + "This is useful if the game got lost in a sea of other messages, or if the game stopped responding")]
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
                            if (gameMessage != null) await gameMessage.ModifyAsync(m => { m.Content = game.GetContent(); m.Embed = game.GetEmbed()?.Build(); }, Utils.DefaultRequestOptions);

                            if (game is PacManGame && Context.BotCan(ChannelPermission.ManageMessages)) await gameMessage.RemoveAllReactionsAsync(Utils.DefaultRequestOptions);
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
        }
    }
}
