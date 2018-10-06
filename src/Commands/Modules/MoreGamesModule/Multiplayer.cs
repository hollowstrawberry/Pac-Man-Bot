using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Net;
using Discord.Commands;
using Discord.WebSocket;
using PacManBot.Games;
using PacManBot.Games.Concrete;
using PacManBot.Extensions;

namespace PacManBot.Commands.Modules
{
    public partial class MoreGamesModule
    {
        private async Task RunMultiplayerGame<TGame>(params SocketUser[] players) where TGame : MultiplayerGame
        {
            var existingGame = Games.GetForChannel(Context.Channel.Id);
            if (existingGame != null)
            {
                await ReplyAsync(existingGame.UserId.Contains(Context.User.Id)
                    ? $"You're already playing a game in this channel!\nUse `{Prefix}cancel` if you want to cancel it."
                    : $"There is already a different game in this channel!\nWait until it's finished or try doing `{Prefix}cancel`");
                return;
            }

            var game = await MultiplayerGame.CreateNew<TGame>(Context.Channel.Id, players, Services);
            Games.Add(game);
            while (!game.AllBots && game.BotTurn) game.BotInput(); // When a bot starts
            var gameMessage = await ReplyAsync(game.GetContent(), game.GetEmbed());
            game.MessageId = gameMessage.Id;

            if (game.AllBots)
            {
                while (game.State == State.Active)
                {
                    try
                    {
                        game.BotInput();
                        if (game.MessageId != gameMessage.Id) gameMessage = await game.GetMessage();
                        game.CancelRequests();
                        if (gameMessage != null) await gameMessage.ModifyAsync(game.GetMessageUpdate(), game.GetRequestOptions());
                    }
                    catch (Exception e) when (e is OperationCanceledException || e is TimeoutException || e is HttpException) { }

                    await Task.Delay(Bot.Random.Next(2000, 3001));
                }

                Games.Remove(game);
            }
        }






        [Command("tictactoe"), Alias("ttt", "tic"), Priority(1)]
        [Remarks("Play Tic-Tac-Toe with another user or the bot")]
        [Summary("You can choose a guild member to invite as an opponent using a mention, username, nickname or user ID. Otherwise, " +
                 "you'll play against the bot.\n\nYou play by sending the number of a free cell (1 to 9) in chat while it is your turn, " +
                 "and to win you must make a line of 3 symbols in any direction\n\n" +
                 "Do `{prefix}cancel` to end the game or `{prefix}bump` to move it to the bottom of the chat. " +
                 "The game times out in case of extended inactivity.\n\n" +
                 "You can also make the bot challenge another user or bot with `{prefix}ttt vs <opponent>`")]
        public async Task StartTicTacToe(SocketGuildUser opponent = null)
            => await RunMultiplayerGame<TTTGame>(opponent ?? (SocketUser)Context.Client.CurrentUser, Context.User);


        [Command("tictactoe vs"), Alias("ttt vs", "tic vs"), Priority(-1), HideHelp]
        [Summary("Make the bot challenge a user... or another bot")]
        public async Task StartTicTacToeVs(SocketGuildUser opponent)
            => await RunMultiplayerGame<TTTGame>(opponent, Context.Client.CurrentUser);




        [Command("5ttt"), Alias("ttt5", "5tictactoe", "5tic"), Priority(1)]
        [Remarks("Play a harder 5-Tic-Tac-Toe with another user or the bot")]
        [Summary("You can choose a guild member to invite as an opponent using a mention, username, nickname or user ID. Otherwise, " +
                 "you'll play against the bot.\n\nYou play by sending the column and row of the cell you want to play, for example, \"C4\". " +
                 "The player who makes the **most lines of 3 symbols** wins. However, if a player makes a lines of **4**, they win instantly.\n\n" +
                 "Do `{prefix}cancel` to end the game or `{prefix}bump` to move it to the bottom of the chat. " +
                 "The game times out in case of extended inactivity.\n\n" +
                 "You can also make the bot challenge another user or bot with `{prefix}5ttt vs <opponent>`")]
        public async Task StartTicTacToe5(SocketGuildUser opponent = null)
            => await RunMultiplayerGame<TTT5Game>((SocketUser)opponent ?? Context.Client.CurrentUser, Context.User);


        [Command("5ttt vs"), Alias("ttt5 vs", "5tictactoe vs", "5tic vs"), Priority(-1), HideHelp]
        [Summary("Make the bot challenge a user... or another bot")]
        public async Task Start5TicTacToeVs(SocketGuildUser opponent)
            => await RunMultiplayerGame<TTT5Game>(opponent, Context.Client.CurrentUser);




        [Command("connect4"), Alias("c4", "four"), Priority(1)]
        [Remarks("Play Connect Four with another user or the bot")]
        [Summary("You can choose a guild member to invite as an opponent using a mention, username, nickname or user ID. Otherwise, " +
                 "you'll play against the bot.\n\n You play by sending the number of a free cell (1 to 7) in chat while it is your turn, " +
                 "and to win you must make a line of 3 symbols in any direction\n\n" +
                 "Do `{prefix}cancel` to end the game or `{prefix}bump` to move it to the bottom of the chat. " +
                 "The game times out in case of extended inactivity.\n\n" +
                 "You can also make the bot challenge another user or bot with `{prefix}c4 vs <opponent>`")]
        public async Task StartConnectFour(SocketGuildUser opponent = null)
            => await RunMultiplayerGame<C4Game>(opponent ?? (SocketUser)Context.Client.CurrentUser, Context.User);


        [Command("connect4 vs"), Alias("c4 vs", "four vs"), Priority(-1), HideHelp]
        [Summary("Make the bot challenge a user... or another bot")]
        public async Task StartConnectFoureVs(SocketGuildUser opponent)
            => await RunMultiplayerGame<C4Game>(opponent, Context.Client.CurrentUser);





        [Command("uno"), Parameters("[players]"), Priority(3)]
        [Remarks("Play Uno with up to 10 friends and bots")]
        [ExampleUsage("uno\nuno @Pac-Man#3944")]
        [Summary("__Tip__: Switching back and forth with DMs to see your cards can be tiresome, " +
                 "so try having your cards open in your phone while you're playing in a computer." +
                 "\n\n__**Commands:**__\n" +
                 "\n • **{prefix}uno** - Starts a new Uno game for up to 10 players. You can specify players and bots as players. " +
                 "Players can join or leave at any time." +
                 "\n • **{prefix}uno bots** - Starts a bot-only game with the specified bots." +
                 "\n • **{prefix}uno join** - Join a game or invite a user or bot." +
                 "\n • **{prefix}uno leave** - Leave the game or kick a bot or inactive user." +
                 "\n • **{prefix}bump** - Move the game to the bottom of the chat." +
                 "\n • **{prefix}cancel** - End the game in the current channel." +
                 "\n\n\n__**Rules:**__\n" +
                 "\n • Each player is given 7 cards." +
                 "\n • The current turn's player must choose to discard a card that matches either the color, number or type of the last card." +
                 "\n • If the player doesn't have any matching card, or they don't want to discard any of their cards, " +
                 "they can say \"**draw**\" to draw a card. That card will be discarded immediately if possible." +
                 "\n • When you only have one card left, you must say \"**uno**\" (You can add it to your card like \"red4 uno\", " +
                 "or you can say it directly after if you forget). If you forget, someone else can say \"uno\" to call you out before the " +
                 "next player plays, and you will draw 2 cards." +
                 "\n • The first player to lose all of their cards wins the game." +
                 "\n • **Special cards:** *Skip* cards make the next player skip a turn. *Reverse* cards change the turn direction, " +
                 "or act like Skip cards with only two players." +
                 " *Draw* cards force the next player to draw cards and skip a turn. *Wild* cards let you choose the color, " +
                 "and will match with any card.")]
        [RequireContext(ContextType.Guild)]
        public async Task StartUno(params SocketGuildUser[] startingPlayers)
            => await RunMultiplayerGame<UnoGame>(new[] { Context.User as SocketGuildUser }.Concatenate(startingPlayers));


        [Command("uno"), Priority(3)]
        [Remarks("Play Uno in a 1v1 match with the bot")]
        [RequireContext(ContextType.DM)]
        public async Task StartUnoDm() => await RunMultiplayerGame<UnoGame>(Context.User, Context.Client.CurrentUser);


        [Command("uno bots"), Alias("uno bot", "unobot", "unobots"), Parameters("[bots]"), HideHelp]
        [Summary("Start a bot-only uno match.")]
        [RequireContext(ContextType.Guild)]
        public async Task StartUnoBots(params SocketGuildUser[] startingPlayers)
        {
            startingPlayers = startingPlayers.Where(x => x.IsBot).ToArray();
            if (startingPlayers.Length < 2) await ReplyAsync("You need to specify at least 2 bots for a bot game.");
            else await RunMultiplayerGame<UnoGame>(startingPlayers);
        }


        [Command("uno help"), Alias("uno h", "uno rules", "uno commands"), Priority(-1), HideHelp]
        [Summary("Gives rules and commands for the Uno game.")]
        public async Task UnoHelp() => await ReplyAsync(Help.MakeHelp("uno", Prefix));


        [Command("uno join"), Alias("uno add", "uno invite"), Priority(-1), HideHelp]
        [Summary("Joins an ongoing Uno game in this channel. Fails if the game is full or if there aren't enough cards to draw for you.\n" +
                 "You can also invite a bot or another user to play.")]
        [RequireContext(ContextType.Guild)]
        public async Task JoinUno(SocketGuildUser user = null)
        {
            bool self = false;
            if (user == null)
            {
                self = true;
                user = Context.User as SocketGuildUser;
            }

            var game = Games.GetForChannel<UnoGame>(Context.Channel.Id);
            if (game == null)
            {
                await ReplyAsync($"There's no Uno game in this channel! Use `{Prefix}uno` to start.");
                return;
            }
            if (game.UserId.Contains(user.Id))
            {
                await ReplyAsync($"{(self ? "You're" : "They're")} already playing!");
                return;
            }
            if (!self && !user.IsBot)
            {
                await ReplyAsync($"{user.Mention} You're being invited to play {game.GameName}. Do `{Prefix}uno join` to join.");
                return;
            }

            string failReason = await game.AddPlayer(user);

            if (failReason != null) await ReplyAsync($"{user.Mention} {"You ".If(self)}can't join this game: {failReason}");
            else await MoveGame();

            if (Context.BotCan(ChannelPermission.ManageMessages)) await Context.Message.DeleteAsync(DefaultOptions);
            else await AutoReactAsync(failReason == null);
        }


        [Command("uno leave"), Alias("uno remove", "uno kick"), Priority(-1), HideHelp]
        [Summary("Leaves the Uno game in this channel.\nYou can also remove a bot or inactive player.")]
        [RequireContext(ContextType.Guild)]
        public async Task LeaveUno(SocketGuildUser user = null)
        {
            bool self = false;
            if (user == null)
            {
                self = true;
                user = Context.User as SocketGuildUser;
            }

            var game = Games.GetForChannel<UnoGame>(Context.Channel.Id);
            if (game == null)
            {
                await ReplyAsync($"There's no Uno game in this channel! Use `{Prefix}uno` to start.");
                return;
            }
            if (!game.UserId.Contains(user.Id))
            {
                await ReplyAsync($"{(self ? "You're" : "They're")} not playing!");
                return;
            }
            if (!self && !user.IsBot && (game.UserId[game.Turn] != user.Id || (DateTime.Now - game.LastPlayed) < TimeSpan.FromMinutes(1)))
            {
                await ReplyAsync("To remove another user they have to be inactive for at least 1 minute during their turn.");
            }

            game.RemovePlayer(user);

            if (game.AllBots || game.UserId.Length < 2) await CancelGame();
            else await MoveGame();

            await AutoReactAsync();
        }
    }
}
