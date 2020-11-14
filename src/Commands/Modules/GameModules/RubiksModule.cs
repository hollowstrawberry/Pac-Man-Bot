using System.Reflection;
using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using PacManBot.Constants;
using PacManBot.Extensions;
using PacManBot.Games.Concrete;

namespace PacManBot.Commands.Modules
{
    [Category(Categories.Games)]
    [RequireBotPermissions(BaseBotPermissions)]
    public class RubiksModule : BaseGameModule<RubiksGame>
    {
        [Command("rubik"), Aliases("rubiks", "rubix")]
        [Description(
            "Gives you a personal Rubik's Cube that you can take to any server or in DMs with the bot.\n\n__**Commands:**__" +
            "\n`rubik [sequence]` - Execute a sequence of turns to apply on the cube." +
            "\n`rubik moves` - Show notation help to control the cube." +
            "\n`rubik scramble` - Scrambles the cube pieces completely." +
            "\n`rubik reset` - Delete the cube, going back to its solved state." +
            "\n`rubik showguide` - Toggle the help displayed below the cube. For pros.")]
        public async Task RubiksCube(CommandContext ctx, [RemainingText]string input = "")
        {
            var game = Game(ctx);
            if (game is null)
            {
                game = StartNewGame(new RubiksGame(ctx.Channel.Id, ctx.User.Id, Services));
            }

            bool removeOld = false;
            switch (input.ToLowerInvariant())
            {
                case "h":
                case "help":
                case "moves":
                case "notation":
                    string moveHelp =
                        $"You can input a sequence of turns using the `rubik [input]` command, " +
                        $"with turns separated by spaces.\nYou can do `help rubik` to see a few more commands.\n\n" +
                        "**Simple turns:** U, D, L, R, F, B\nThese are the basic clockwise turns of the cube. " +
                        "They stand for the Up, Down, Left, Right, Front and Back sides.\n" +
                        "**Counterclockwise turns:** Add `'`. Example: U', R'\n" +
                        "**Double turns:** Add `2`. Example: F2, D2\n" +
                        "**Wide turns:** Add `w`. Example: Dw, Lw2, Uw'\n" +
                        "These rotate two layers at the same time in the direction of the given face.\n\n" +
                        "**Slice turns:** M E S\n" +
                        "These rotate the middle layer corresponding with L, D and B respectively.\n\n" +
                        "**Cube rotations:** x, y, z\n" +
                        "These rotate the entire cube in the direction of R, U and F respectively. " +
                        "They can also be counterclockwise or double.";

                    await ctx.RespondAsync(moveHelp);
                    return;


                case "reset":
                case "solve":
                    EndGame(ctx);
                    await ctx.AutoReactAsync();
                    return;


                case "scramble":
                case "shuffle":
                    game.Scramble();
                    removeOld = true;
                    break;


                case "showguide":
                    game.ShowHelp = !game.ShowHelp;
                    if (game.ShowHelp) await ctx.AutoReactAsync();
                    else await ctx.RespondAsync("❗ You just disabled the help displayed below the cube.\n" +
                                                "Consider re-enabling it if you're not used to the game.");
                    break;


                default:
                    if (!string.IsNullOrEmpty(input))
                    {
                        if (!game.TryDoMoves(input))
                        {
                            await ctx.RespondAsync($"{CustomEmoji.Cross} Invalid sequence of moves. " +
                                $"Do `{Storage.GetPrefix(ctx)}help rubik` for commands.");
                            return;
                        }
                    }
                    removeOld = true;
                    break;
            }

            await SaveGameAsync(ctx);
            if (removeOld && game.ChannelId == ctx.Channel.Id) await DeleteGameMessageAsync(ctx);
            await RespondGameAsync(ctx);
        }
    }
}