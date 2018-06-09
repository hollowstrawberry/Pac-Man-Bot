using System;
using System.Linq;
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
    public partial class MoreGamesModule : ModuleBase<SocketCommandContext>
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




        [Command("uno"), Parameters("[players]"), Priority(-1)]
        [Remarks("Play Uno with up to 10 friends and bots")]
        [ExampleUsage("uno\nuno @Pac-Man#3944")]
        [Summary("__Tip__: Switching back and forth with DMs to see your cards can be tiresome, so try having your cards open in your phone while you're playing in a computer."
               + "\n\n__**Commands:**__\n"
               + "\n • **{prefix}uno** - Starts a new Uno game, for up to 10 players. You can specify players and bots as opponents. Players can join or leave at any time."
               + "\n • **{prefix}uno join** - Join a game or invite a user or bot."
               + "\n • **{prefix}uno leave** - Leave the game or kick a bot or inactive user."
               + "\n • **{prefix}bump** - Move the game to the bottom of the chat."
               + "\n • **{prefix}cancel** - End the game in the current channel."
               + "\nᅠ{division}\n__**Rules:**__\n"
               + "\n • Each player is given 7 cards."
               + "\n • The current turn's player must choose to discard a card that matches either the color, number or type of the last card."
               + "\n • If the player doesn't have any matching card, they will draw another card. If they still can't play they will skip a turn."
               + "\n • When you only have one card left, __you must say \"uno\"__. If you don't, someone else can call you out by saying \"uno\" __before the next player plays__, and you will draw 2 cards."
               + "\n • The first player to lose all of their cards wins the game."
               + "\n • **Special cards:** *Skip* cards make the next player skip a turn. *Reverse* cards change the turn direction, or act like Skip cards with only two players."
               + " *Draw* cards force the next player to draw cards and skip a turn. *Wild* cards let you choose the color, and will match with any card."
               + "\nᅠ")]
        [RequireContext(ContextType.Guild)]
        [BetterRequireBotPermission(ChannelPermission.ReadMessageHistory | ChannelPermission.UseExternalEmojis | ChannelPermission.EmbedLinks)]
        public async Task StartUno(params SocketGuildUser[] startingPlayers)
        {
            await RunMultiplayerGame<UnoGame>(Utils.ArrayConcat(new SocketGuildUser[] { Context.User as SocketGuildUser }, startingPlayers));
        }


        [Command("uno help"), Alias("uno h", "uno rules", "uno commands"), Priority(1), HideHelp]
        [Summary("Gives rules and commands for the Uno game.")]
        [RequireContext(ContextType.Guild)]
        public async Task UnoHelp()
        {
            var summary = typeof(MoreGamesModule).GetMethod(nameof(StartUno)).GetCustomAttributes(typeof(SummaryAttribute), false).FirstOrDefault() as SummaryAttribute;
            await ReplyAsync(summary?.Text.Replace("{prefix}", storage.GetPrefix(Context.Guild)).Replace("{division}", "") ?? "Couldn't get help", options: Utils.DefaultOptions);
        }


        [Command("uno join"), Alias("uno add", "uno invite"), Priority(1), HideHelp]
        [Summary("Joins an ongoing Uno game in this channel. Fails if the game is full or if there aren't enough cards to draw for you.\n"
               + "You can also invite a bot or another user to play.")]
        [RequireContext(ContextType.Guild)]
        [BetterRequireBotPermission(ChannelPermission.ReadMessageHistory | ChannelPermission.UseExternalEmojis | ChannelPermission.EmbedLinks)]
        public async Task JoinUno(SocketGuildUser user = null)
        {
            bool self = false;
            if (user == null)
            {
                self = true;
                user = Context.User as SocketGuildUser;
            }

            var game = storage.GetGame<UnoGame>(Context.Channel.Id);
            if (game == null)
            {
                await ReplyAsync($"There's no Uno game in this channel! Use **{storage.GetPrefix(Context.Guild)}uno** to start.", options: Utils.DefaultOptions);
                return;
            }
            if (game.UserId.Contains(user.Id))
            {
                await ReplyAsync($"{(self ? "You're" : "They're")} already playing!", options: Utils.DefaultOptions);
                return;
            }
            if (!self && !user.IsBot)
            {
                await ReplyAsync($"{user.Mention} You're being invited to play {game.Name}. Do **{storage.GetPrefix(Context.Guild)}uno join** to join.", options: Utils.DefaultOptions);
                return;
            }

            string failReason = game.AddPlayer(user.Id);
            if (failReason != null) await ReplyAsync($"{user.Mention} {"You ".If(self)}can't join this game: {failReason}", options: Utils.DefaultOptions);
            else await MoveGame();

            if (Context.BotCan(ChannelPermission.ManageMessages)) await Context.Message.DeleteAsync(options: Utils.DefaultOptions);
            else await Context.Message.AddReactionAsync(failReason == null ? CustomEmoji.ECheck : CustomEmoji.ECross, Utils.DefaultOptions);
        }


        [Command("uno leave"), Alias("uno remove", "uno kick"), Priority(1), HideHelp]
        [Summary("Leaves the Uno game in this channel.\nYou can also remove a bot or inactive player.")]
        [RequireContext(ContextType.Guild)]
        [BetterRequireBotPermission(ChannelPermission.ReadMessageHistory | ChannelPermission.UseExternalEmojis | ChannelPermission.EmbedLinks)]
        public async Task LeaveUno(SocketGuildUser user = null)
        {
            bool self = false;
            if (user == null)
            {
                self = true;
                user = Context.User as SocketGuildUser;
            }

            var game = storage.GetGame<UnoGame>(Context.Channel.Id);
            if (game == null)
            {
                await ReplyAsync($"There's no Uno game in this channel! Use **{storage.GetPrefix(Context.Guild)}uno** to start.", options: Utils.DefaultOptions);
                return;
            }
            if (!game.UserId.Contains(user.Id))
            {
                await ReplyAsync($"{(self ? "You're" : "They're")} not playing!", options: Utils.DefaultOptions);
                return;
            }
            if (game.UserId.Length <= 2)
            {
                await CancelGame();
                return;
            }
            if (!self && !user.IsBot && (game.UserId[(int)game.Turn] != user.Id || (DateTime.Now - game.LastPlayed) < TimeSpan.FromMinutes(1)))
            {
                await ReplyAsync("To remove another user they have to be inactive for at least 1 minute during their turn.");
            }

            game.RemovePlayer(user.Id);
            await MoveGame();
            await Context.Message.AddReactionAsync(CustomEmoji.ECheck, Utils.DefaultOptions);
        }




        [Command("tictactoe"), Alias("ttt", "tic"), Priority(-1)]
        [Remarks("Play Tic-Tac-Toe with another user or the bot")]
        [Summary("You can choose a guild member to invite as an opponent using a mention, username, nickname or user ID. Otherwise, you'll play against the bot.\n\n"
               + "You play by sending the number of a free cell (1 to 9) in chat while it is your turn, and to win you must make a line of 3 symbols in any direction\n\n"
               + "Do **{prefix}cancel** to end the game or **{prefix}bump** to move it to the bottom of the chat. The game times out after 5 minutes of inactivity.\n\n"
               + "You can also make the bot challenge another user or bot with **{prefix}ttt vs <opponent>**")]
        [BetterRequireBotPermission(ChannelPermission.ReadMessageHistory | ChannelPermission.UseExternalEmojis | ChannelPermission.EmbedLinks)]
        public async Task StartTicTacToe(SocketGuildUser opponent = null)
        {
            await RunMultiplayerGame<TTTGame>(opponent ?? (IUser)Context.Client.CurrentUser, Context.User);
        }

        [Command("tictactoe vs"), Alias("ttt vs", "tic vs"), Priority(1), HideHelp]
        [Summary("Make the bot challenge a user... or another bot")]
        [BetterRequireBotPermission(ChannelPermission.ReadMessageHistory | ChannelPermission.UseExternalEmojis | ChannelPermission.EmbedLinks)]
        public async Task StartTicTacToeVs(SocketGuildUser opponent)
        {
            await RunMultiplayerGame<TTTGame>(opponent, Context.Client.CurrentUser);
        }


        [Command("5ttt"), Alias("ttt5", "5tictactoe", "5tic"), Priority(-1)]
        [Remarks("Play a harder 5-Tic-Tac-Toe with another user or the bot")]
        [Summary("You can choose a guild member to invite as an opponent using a mention, username, nickname or user ID. Otherwise, you'll play against the bot.\n\n"
               + "You play by sending the column and row of the cell you want to play, for example, \"C4\". The player who makes the **most lines of 3 symbols** wins. "
               + "However, if a player makes a lines of **4**, they win instantly.\n\n"
               + "Do **{prefix}cancel** to end the game or **{prefix}bump** to move it to the bottom of the chat. The game times out after 5 minutes of inactivity.\n\n"
               + "You can also make the bot challenge another user or bot with **{prefix}5ttt vs <opponent>**")]
        [BetterRequireBotPermission(ChannelPermission.ReadMessageHistory | ChannelPermission.UseExternalEmojis | ChannelPermission.EmbedLinks)]
        public async Task StartTicTacToe5(SocketGuildUser opponent = null)
        {
            await RunMultiplayerGame<TTT5Game>(opponent ?? (IUser)Context.Client.CurrentUser, Context.User);
        }

        [Command("5ttt vs"), Alias("ttt5 vs", "5tictactoe vs", "5tic vs"), Priority(1), HideHelp]
        [Summary("Make the bot challenge a user... or another bot")]
        [BetterRequireBotPermission(ChannelPermission.ReadMessageHistory | ChannelPermission.UseExternalEmojis | ChannelPermission.EmbedLinks)]
        public async Task Start5TicTacToeVs(SocketGuildUser opponent)
        {
            await RunMultiplayerGame<TTT5Game>(opponent, Context.Client.CurrentUser);
        }


        [Command("connect4"), Alias("c4", "four"), Priority(-1)]
        [Remarks("Play Connect Four with another user or the bot")]
        [Summary("You can choose a guild member to invite as an opponent using a mention, username, nickname or user ID. Otherwise, you'll play against the bot.\n\n"
               + "You play by sending the number of a free cell (1 to 7) in chat while it is your turn, and to win you must make a line of 3 symbols in any direction\n\n"
               + "Do **{prefix}cancel** to end the game or **{prefix}bump** to move it to the bottom of the chat. The game times out after 5 minutes of inactivity.\n\n"
               + "You can also make the bot challenge another user or bot with **{prefix}c4 vs <opponent>**")]
        [BetterRequireBotPermission(ChannelPermission.ReadMessageHistory | ChannelPermission.UseExternalEmojis | ChannelPermission.EmbedLinks)]
        public async Task StartConnectFour(SocketGuildUser opponent = null)
        {
            await RunMultiplayerGame<C4Game>(opponent ?? (IUser)Context.Client.CurrentUser, Context.User);
        }

        [Command("connect4 vs"), Alias("c4 vs", "four vs"), Priority(1), HideHelp]
        [Summary("Make the bot challenge a user... or another bot")]
        [BetterRequireBotPermission(ChannelPermission.ReadMessageHistory | ChannelPermission.UseExternalEmojis | ChannelPermission.EmbedLinks)]
        public async Task StartConnectFoureVs(SocketGuildUser opponent)
        {
            await RunMultiplayerGame<C4Game>(opponent, Context.Client.CurrentUser);
        }




        private async Task RunMultiplayerGame<TGame>(params IUser[] players) where TGame : MultiplayerGame
        {
            foreach (var otherGame in storage.Games)
            {
                if (otherGame.ChannelId == Context.Channel.Id)
                {
                    await ReplyAsync(otherGame.UserId.Contains(Context.User.Id) ?
                        $"You're already playing a game in this channel!\nUse **{storage.GetPrefixOrEmpty(Context.Guild)}cancel** if you want to cancel it." :
                        $"There is already a different game in this channel!\nWait until it's finished or try doing **{storage.GetPrefixOrEmpty(Context.Guild)}cancel**");
                    return;
                }
            }

            var playerIds = players.Select(x => x.Id).ToArray();

            TGame game = MultiplayerGame.New<TGame>(Context.Channel.Id, playerIds, shardedClient, logger, storage);

            while (!game.AllBots && game.AITurn) game.DoTurnAI();

            storage.AddGame(game);

            IUserMessage gameMessage;
            try
            {
                gameMessage = await ReplyAsync(game.GetContent(), false, game.GetEmbed()?.Build(), Utils.DefaultOptions);
            }
            catch (HttpException e)
            {
                storage.DeleteGame(game);
                await logger.Log(LogSeverity.Error, e.Reason); // Let's hope I figure out why I get Bad Gateway
                throw e;
            }
            game.MessageId = gameMessage.Id;

            while (game.State == State.Active)
            {
                if (game.AllBots)
                {
                    try
                    {
                        game.DoTurnAI();
                        if (game.MessageId != gameMessage.Id) gameMessage = await game.GetMessage();
                        game.CancelRequests();
                        if (gameMessage != null) await gameMessage.ModifyAsync(game.UpdateMessage, game.RequestOptions);
                    }
                    catch (Exception e) when (e is OperationCanceledException || e is TimeoutException || e is HttpException) { }

                    await Task.Delay(Bot.Random.Next(1000, 2001));
                }
                else await Task.Delay(5000);

                if ((DateTime.Now - game.LastPlayed) > game.Expiry)
                {
                    game.State = State.Cancelled;
                    storage.DeleteGame(game);
                    try
                    {
                        if (gameMessage.Id != game.MessageId) gameMessage = await game.GetMessage();
                        if (gameMessage != null) await gameMessage.ModifyAsync(game.UpdateMessage, Utils.DefaultOptions);
                    }
                    catch (HttpException) { } // Something happened to the message, we can ignore it
                    return;
                }
            }

            if (storage.Games.Contains(game)) storage.DeleteGame(game); // When playing against the bot
        }




        [Command("bump"), Alias("b", "refresh", "r", "move")]
        [Remarks("Move any game to the bottom of the chat")]
        [Summary("Moves the current game's message in this channel to the bottom of the chat, deleting the old one."
               + "This is useful if the game got lost in a sea of other messages, or if the game stopped responding")]
        [BetterRequireBotPermission(ChannelPermission.ReadMessageHistory | ChannelPermission.UseExternalEmojis | ChannelPermission.EmbedLinks | ChannelPermission.AddReactions)]
        private async Task MoveGame()
        {
            var game = storage.GetGame(Context.Channel.Id);
            if (game == null)
            {
                await ReplyAsync("There is no active game in this channel!", options: Utils.DefaultOptions);
                return;
            }

            try
            {
                var gameMessage = await game.GetMessage();
                if (gameMessage != null) await gameMessage.DeleteAsync(Utils.DefaultOptions); // Old message
            }
            catch (HttpException) { } // Something happened to the message, can ignore it

            var message = await ReplyAsync(game.GetContent(), false, game.GetEmbed()?.Build(), Utils.DefaultOptions);
            game.MessageId = message.Id;

            if (game is PacManGame pacManGame) await PacManModule.AddControls(pacManGame, message);
        }


        [Command("cancel"), Alias("end")]
        [Remarks("Cancel any game you're playing. Always usable by moderators")]
        [Summary("Cancels the current game in this channel, but only if you started or if nobody has played in over a minute. Always usable by users with the Manage Messages permission.")]
        [BetterRequireBotPermission(ChannelPermission.ReadMessageHistory | ChannelPermission.UseExternalEmojis | ChannelPermission.EmbedLinks | ChannelPermission.AddReactions)]
        public async Task CancelGame()
        {
            var game = storage.GetGame(Context.Channel.Id);
            if (game == null)
            {
                await ReplyAsync("There is no active game in this channel!", options: Utils.DefaultOptions);
                return;
            }

            if (game.UserId.Contains(Context.User.Id) || Context.UserCan(ChannelPermission.ManageMessages) || DateTime.Now - game.LastPlayed > TimeSpan.FromSeconds(60)
                || game is MultiplayerGame tpGame && tpGame.AllBots)
            {
                game.State = State.Cancelled;
                storage.DeleteGame(game);

                try
                {
                    var gameMessage = await game.GetMessage();
                    if (gameMessage != null)
                    {
                        await gameMessage.ModifyAsync(game.UpdateMessage, Utils.DefaultOptions);
                        if (game is PacManGame && Context.BotCan(ChannelPermission.ManageMessages)) await gameMessage.RemoveAllReactionsAsync(Utils.DefaultOptions);
                    }
                }
                catch (HttpException) { } // Something happened to the message, we can ignore it

                if (game is PacManGame pacManGame && Context.Guild != null)
                {
                    await ReplyAsync($"Game ended. Score won't be registered.\n**Result:** {pacManGame.score} points in {pacManGame.Time} turns", options: Utils.DefaultOptions);
                }
                else await Context.Message.AddReactionAsync(CustomEmoji.ECheck, Utils.DefaultOptions);
            }
            else await ReplyAsync("You can't cancel this game because someone else is still playing! Try again in a minute.", options: Utils.DefaultOptions);
        }
    }
}
