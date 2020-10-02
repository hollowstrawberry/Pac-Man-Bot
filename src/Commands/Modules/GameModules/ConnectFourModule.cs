using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using PacManBot.Games.Concrete;

namespace PacManBot.Commands.Modules
{
    [Module(ModuleNames.Games)]
    [RequireBotPermissions(BaseBotPermissions)]
    public class ConnectFourModule : MultiplayerGameModule<C4Game>
    {
        [Command("connect4"), Aliases("c4", "four"), Priority(1)]
        [Description(
            "You can choose a guild member to invite as an opponent using a mention, username, nickname or user ID." +
            "Otherwise, you'll play against the bot.\n\n You play by sending the number of a free cell (1 to 7) " +
            "in chat while it is your turn, and to win you must make a line of 3 symbols in any direction\n\n" +
            "Do `{prefix}cancel` to end the game or `{prefix}bump` to move it to the bottom of the chat. " +
            "The game times out in case of extended inactivity.\n\n" +
            "You can also make the bot challenge another user or bot with `{prefix}c4vs <opponent>`")]
        public async Task StartConnectFour(CommandContext ctx, DiscordUser opponent = null)
        {
            await StartNewMPGameAsync(ctx, opponent ?? ctx.Client.CurrentUser, ctx.User);
        }


        [Command("connect4vs"), Aliases("c4vs", "fourvs"), Priority(-1), Hidden]
        [Description("Make the bot challenge a user... or another bot")]
        public async Task StartConnectFourVs(CommandContext ctx, DiscordUser opponent)
        {
            await StartNewMPGameAsync(ctx, opponent, ctx.Client.CurrentUser);
        }
    }
}
