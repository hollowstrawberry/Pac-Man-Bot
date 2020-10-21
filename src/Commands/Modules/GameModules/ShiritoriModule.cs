using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using PacManBot.Constants;
using PacManBot.Extensions;
using PacManBot.Games;
using PacManBot.Games.Concrete;

namespace PacManBot.Commands.Modules
{
    [Category(Categories.Games)]
    [Group("shiritori"), Aliases("shiri")]
    [Description("Play Shiritori alone or with friends\n" +
        "This is a game where you must enter a word starting with the last letter of the last word.\n" +
        "Do it within the time limit or you'll lose!")]
    [RequireBotPermissions(BaseBotPermissions)]
    public class ShiritoriModule : BaseMultiplayerModule<ShiritoriGame>
    {
        [GroupCommand, Priority(3)]
        public async Task StartShiritori(CommandContext ctx)
        {
            await StartNewMPGameAsync(ctx, ctx.User);

            var game = Game(ctx);
            if (game == null) return;

            while (game.VisualTimeRemaining > 0)
            {
                var lastPlayed = game.LastPlayed;
                await Task.Delay(1000);
                if (game.LastPlayed <= lastPlayed)
                {
                    game.VisualTimeRemaining -= 1;
                    if (game.VisualTimeRemaining > 0) await UpdateGameMessageAsync(ctx);
                }
            }

            game.State = GameState.Lose;
            await UpdateGameMessageAsync(ctx);
            EndGame(ctx);
        }
    }
}
