using System.Reflection;
using System.Threading.Tasks;
using Discord.Net;
using Discord.Commands;
using PacManBot.Games;
using PacManBot.Constants;

namespace PacManBot.Commands
{
    public partial class MoreGamesModule
    {
        [Command("rubik"), Alias("rubiks", "rubix", "rb", "rbx")]
        [Remarks("Your personal rubik's cube")]
        [Summary("Gives you a personal Rubik's Cube that you can take to any server or in DMs with the bot.\n\n__**Commands:**__" +
                 "\n**{prefix}rubik [sequence]** - Execute a sequence of turns to apply on the cube." +
                 "\n**{prefix}rubik moves** - Show notation help to control the cube." +
                 "\n**{prefix}rubik scramble** - Scrambles the cube pieces completely." +
                 "\n**{prefix}rubik reset** - Delete the cube, going back to its solved state." +
                 "\n**{prefix}rubik showguide** - Toggle the help displayed below the cube. For pros.")]
        public async Task RubiksCube([Remainder] string input = "")
        {
            var cube = Storage.GetUserGame<RubiksGame>(Context.User.Id);

            if (cube == null)
            {
                cube = new RubiksGame(Context.Channel.Id, Context.User.Id, Services);
                Storage.AddGame(cube);
            }

            bool removeOld = false;
            switch (input.ToLower())
            {
                case "moves":
                case "notation":
                    string help =
                        $"You can give a sequence of turns using the **{Prefix}rubik** command, " +
                        $"with turns separated by spaces.\nYou can do **{Prefix}rubik help** for a few more commands.\n\n" +
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

                    await ReplyAsync(help);
                    return;


                case "h":
                case "help":
                    var summary = typeof(MoreGamesModule).GetMethod(nameof(RubiksCube)).GetCustomAttribute<SummaryAttribute>();
                    await ReplyAsync(summary.Text.Replace("{prefix}", $"{Prefix}"));
                    return;


                case "reset":
                    Storage.DeleteGame(cube);
                    await AutoReactAsync();
                    return;


                case "scramble":
                case "shuffle":
                    cube.Scramble();
                    removeOld = true;
                    break;


                case "showguide":
                    cube.showHelp = !cube.showHelp;
                    if (cube.showHelp) await AutoReactAsync();
                    else await ReplyAsync("❗ You just disabled the help displayed below the cube.\n" +
                                          "Consider re-enabling it if you're not used to the game.");
                    break;


                default:
                    if (!string.IsNullOrEmpty(input))
                    {
                        if (!cube.DoMoves(input))
                        {
                            await ReplyAsync($"{CustomEmoji.Cross} Invalid sequence of moves. " +
                                             $"Do **{Prefix}rubik help** for commands.");
                            return;
                        }
                    }
                    removeOld = true;
                    break;
            }

            var oldMessage = await cube.GetMessage();
            var newMessage = await ReplyAsync(cube.GetContent(), cube.GetEmbed(Context.Guild));
            cube.MessageId = newMessage.Id;
            cube.ChannelId = Context.Channel.Id;

            if (removeOld && oldMessage != null && oldMessage.Channel.Id == Context.Channel.Id)
            {
                try { await oldMessage.DeleteAsync(DefaultOptions); }
                catch (HttpException) { }
            }
        }
    }
}
