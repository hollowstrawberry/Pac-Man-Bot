using System.Linq;
using System.Threading.Tasks;
using Discord.Net;
using Discord.Commands;
using PacManBot.Games;

namespace PacManBot.Modules
{
    partial class MoreGamesModule
    {
        [Command("rubik"), Alias("rubiks", "rubix", "rb", "rbx")]
        [Remarks("Your personal rubik's cube")]
        [Summary("Gives you a personal Rubik's Cube that you can take to any server or in DMs with the bot.\n\n__**Commands:**__"
               + "\n**{prefix}rubik [sequence]** - Execute a sequence of turns to apply on the cube."
               + "\n**{prefix}rubik moves** - Show notation help to control the cube."
               + "\n**{prefix}rubik scramble** - Scrambles the cube pieces completely."
               + "\n**{prefix}rubik reset** - Delete the cube, going back to its solved state."
               + "\n**{prefix}rubik showguide** - Toggle the help displayed below the cube. For pros.")]
        public async Task RubiksCube([Remainder] string input = "")
        {
            var cube = storage.GetUserGame<RubiksGame>(Context.User.Id);

            if (cube == null)
            {
                cube = new RubiksGame(Context.User.Id, shardedClient, logger, storage);
                storage.AddUserGame(cube);
            }

            bool removeOld = false;
            string prefix = storage.GetPrefixOrEmpty(Context.Guild);
            switch (input.ToLower())
            {
                case "moves":
                case "notation":
                    string help = $"You can give a sequence of turns using the **{prefix}rubik** command, " +
                                  $"with turns separated by spaces.\nYou can do **{prefix}rubik help** for a few more commands.\n\n" +
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

                    await ReplyAsync(help, options: Bot.DefaultOptions);
                    return;


                case "h":
                case "help":
                    var summary = typeof(MoreGamesModule).GetMethod(nameof(RubiksCube)).GetCustomAttributes(typeof(SummaryAttribute), false)
                        .FirstOrDefault() as SummaryAttribute;
                    await ReplyAsync(summary.Text.Replace("{prefix}", $"{prefix}"), options: Bot.DefaultOptions);
                    return;


                case "reset":
                    storage.DeleteUserGame(cube);
                    cube = null;
                    await Context.Message.AddReactionAsync(CustomEmoji.ECheck, Bot.DefaultOptions);
                    return;


                case "scramble":
                    cube.Scramble();
                    removeOld = true;
                    break;


                case "showguide":
                    cube.showHelp = !cube.showHelp;
                    if (cube.showHelp) await Context.Message.AddReactionAsync(CustomEmoji.ECheck, Bot.DefaultOptions);
                    else await ReplyAsync("❗ You just disabled the help displayed below the cube.\n" +
                                          "Consider re-enabling it if you're not used to the game.", options: Bot.DefaultOptions);
                    break;


                default:
                    if (!string.IsNullOrEmpty(input))
                    {
                        if (!cube.DoMoves(input))
                        {
                            await ReplyAsync($"{CustomEmoji.Cross} Invalid sequence of moves. Try **{prefix}rubik notation** for help.");
                            return;
                        }
                    }
                    removeOld = true;
                    break;
            }

            var oldMessage = cube.message;
            cube.message = await ReplyAsync(cube.GetContent(), false, cube.GetEmbed(Context.Guild).Build(), Bot.DefaultOptions);

            if (removeOld && oldMessage != null && oldMessage.Channel == Context.Channel)
            {
                try { await oldMessage.DeleteAsync(Bot.DefaultOptions); }
                catch (HttpException) {;}
            }
        }
    }
}
