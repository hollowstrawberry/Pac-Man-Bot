using System.Threading.Tasks;
using Discord.Commands;
using Discord.WebSocket;
using PacManBot.Games.Concrete;

namespace PacManBot.Commands.Modules.GameModules
{
    [Name(ModuleNames.Games), Remarks("3")]
    public class TTT5Module : MultiplayerGameModule<TTT5Game>
    {
        [Command("5ttt"), Alias("ttt5", "5tictactoe", "5tic"), Priority(1)]
        [Remarks("Play a harder 5-Tic-Tac-Toe with another user or the bot")]
        [Summary("You can choose a guild member to invite as an opponent using a mention, username, nickname or user ID. Otherwise, " +
                 "you'll play against the bot.\n\nYou play by sending the column and row of the cell you want to play, for example, \"C4\". " +
                 "The player who makes the **most lines of 3 symbols** wins. However, if a player makes a lines of **4**, they win instantly.\n\n" +
                 "Do `{prefix}cancel` to end the game or `{prefix}bump` to move it to the bottom of the chat. " +
                 "The game times out in case of extended inactivity.\n\n" +
                 "You can also make the bot challenge another user or bot with `{prefix}5ttt vs <opponent>`")]
        public async Task StartTicTacToe5(SocketGuildUser opponent = null)
        {
            await RunGameAsync((SocketUser)opponent ?? Context.Client.CurrentUser, Context.User);
        }


        [Command("5ttt vs"), Alias("ttt5 vs", "5tictactoe vs", "5tic vs"), Priority(-1), HideHelp]
        [Summary("Make the bot challenge a user... or another bot")]
        public async Task Start5TicTacToeVs(SocketGuildUser opponent)
        {
            await RunGameAsync(opponent, Context.Client.CurrentUser);
        }
    }
}
