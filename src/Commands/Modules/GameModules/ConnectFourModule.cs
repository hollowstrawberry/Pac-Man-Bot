using System.Threading.Tasks;
using Discord.Commands;
using Discord.WebSocket;
using PacManBot.Games.Concrete;

namespace PacManBot.Commands.Modules
{
    [Name(ModuleNames.Games), Remarks("3")]
    public class ConnectFourModule : MultiplayerGameModule<C4Game>
    {
        [Command("connect4"), Alias("c4", "four"), Priority(1)]
        [Remarks("Play Connect Four with another user or the bot")]
        [Summary("You can choose a guild member to invite as an opponent using a mention, username, nickname or user ID. Otherwise, " +
                 "you'll play against the bot.\n\n You play by sending the number of a free cell (1 to 7) in chat while it is your turn, " +
                 "and to win you must make a line of 3 symbols in any direction\n\n" +
                 "Do `{prefix}cancel` to end the game or `{prefix}bump` to move it to the bottom of the chat. " +
                 "The game times out in case of extended inactivity.\n\n" +
                 "You can also make the bot challenge another user or bot with `{prefix}c4 vs <opponent>`")]
        public async Task StartConnectFour(SocketGuildUser opponent = null)
        {
            await RunGameAsync(opponent ?? (SocketUser)Context.Client.CurrentUser, Context.User);
        }


        [Command("connect4 vs"), Alias("c4 vs", "four vs"), Priority(-1), HideHelp]
        [Summary("Make the bot challenge a user... or another bot")]
        public async Task StartConnectFourVs(SocketGuildUser opponent)
        {
            await RunGameAsync(opponent, Context.Client.CurrentUser);
        }
    }
}
