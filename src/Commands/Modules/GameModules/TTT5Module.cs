using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using PacManBot.Games.Concrete;

namespace PacManBot.Commands.Modules
{
    [Module(ModuleNames.Games)]
    [RequireBotPermissions(BaseBotPermissions)]
    public class TTT5Module : MultiplayerGameModule<TTT5Game>
    {
        [Command("5ttt"), Aliases("ttt5", "5tictactoe", "5tic"), Priority(1)]
        [Description(
            "You can choose a guild member to invite as an opponent using a mention, username, nickname or user ID. Otherwise, " +
            "you'll play against the bot.\n\nYou play by sending the column and row of the cell you want to play, for example, \"C4\". " +
            "The player who makes the **most lines of 3 symbols** wins. However, if a player makes a lines of **4**, they win instantly.\n\n" +
            "Do `cancel` to end the game or `bump` to move it to the bottom of the chat. " +
            "The game times out in case of extended inactivity.\n\n")]
        public async Task StartTicTacToe5(CommandContext ctx, DiscordUser opponent = null)
        {
            await StartNewMPGameAsync(ctx, opponent ?? ctx.Client.CurrentUser, ctx.User);
        }
    }
}
