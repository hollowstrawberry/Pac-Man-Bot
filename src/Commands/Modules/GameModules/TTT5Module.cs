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
            "Do `{prefix}cancel` to end the game or `{prefix}bump` to move it to the bottom of the chat. " +
            "The game times out in case of extended inactivity.\n\n" +
            "You can also make the bot challenge another user or bot with `{prefix}5tttvs <opponent>`")]
        public async Task StartTicTacToe5(CommandContext ctx, DiscordUser opponent = null)
        {
            await StartNewMPGameAsync(ctx, opponent ?? ctx.Client.CurrentUser, ctx.User);
        }


        [Command("5tttvs"), Aliases("ttt5vs", "5tictactoevs", "5ticvs"), Priority(-1), Hidden]
        [Description("Make the bot challenge a user... or another bot")]
        public async Task Start5TicTacToeVs(CommandContext ctx, DiscordUser opponent)
        {
            await StartNewMPGameAsync(ctx, opponent, ctx.Client.CurrentUser);
        }
    }
}
