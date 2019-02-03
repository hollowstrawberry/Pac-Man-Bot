using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Net;
using PacManBot.Constants;
using PacManBot.Extensions;
using PacManBot.Games;
using PacManBot.Games.Concrete;

namespace PacManBot.Commands.Modules.GameModules
{
    [Name(ModuleNames.Games), Remarks("3")]
    public class PacManModule : BaseGameModule<PacManGame>
    {
        private const int MaxDisplayedScores = 20;


        [Command("pacman"), Alias("p", "start"), Parameters("[mobile]"), Priority(10)]
        [Remarks("Start a new game in this channel")]
        [Summary("Starts a new Pac-Man game in this channel.\nYou can add \"slim\" or \"s\" " +
                 "after the command to use **Slim Mode**, which fits better on phones. If slim mode is still too wide, " +
                 "you could reduce the font size in your phone's settings, if you want to." +
                 "Use **{prefix}display** to change display modes later.\n" +
                 "You can also play a custom Pac-Man map with the command **{prefix}custompacman**\n\n" +
                 "Use **{prefix}bump** to move the game message to the bottom of the chat. Use **{prefix}cancel** to end the game. ")]
        public async Task StartGame(string arg = "")
        {
            if (await CheckGameAlreadyExistsAsync()) return;

            arg = arg.ToLowerInvariant();
            bool mobile = arg.StartsWith("s") || arg.StartsWith("m");

            StartNewGame(new PacManGame(Context.Channel.Id, Context.User.Id, null, mobile, Services));
            var msg = await ReplyGameAsync(Game.GetContent(showHelp: false) + "```diff\n+Starting game```");
            await AddControls(Game, msg);
        }


        [Command("custompacman"), Alias("pacmancustom"), Priority(9)]
        [Summary("Starts a new Pac-Man game in this channel using the provided custom map.\n" +
                 "Use **{prefix}custompacman** by itself to see a guide for custom maps.\n\n" +
                 "Use **{prefix}display** to switch between normal mode and slim mode. " +
                 "Use **{prefix}bump** to move the game message to the bottom of the chat. Use **{prefix}cancel** to end the game.")]
        public async Task StartCustomGame([Remainder]string map = null)
        {
            if (map == null)
            {
                string message = Content.customHelp.Replace("{prefix}", Context.Prefix);

                var embed = new EmbedBuilder { Color = Colors.PacManYellow };
                foreach (var (name, url) in Content.customLinks)
                {
                    embed.AddField(name, $"[Click here]({url} \"{url}\")", true);
                }

                await ReplyAsync(message, embed);
                return;
            }

            if (await CheckGameAlreadyExistsAsync()) return;

            map = map.ExtractCode();

            PacManGame newGame;
            try
            {
                newGame = new PacManGame(Context.Channel.Id, Context.User.Id, map, false, Services);
            }
            catch (InvalidMapException e)
            {
                await ReplyAsync(
                    $"That's not a valid map!: {e.Message}.\n" +
                    $"Use `{Context.Prefix}custompacman` by itself for a guide on custom maps.");
                return;
            }

            StartNewGame(newGame);

            var msg = await ReplyGameAsync(Game.GetContent(showHelp: false) + "```diff\n+Starting game```");
            await AddControls(Game, msg);
        }




        public static async Task AddControls(PacManGame game, IUserMessage message)
        {
            try
            {
                var requestOptions = game.GetRequestOptions(); // So the edit can be cancelled

                foreach (var input in PacManGame.GameInputs.Keys)
                {
                    if (game.State != GameState.Active) break;
                    await message.AddReactionAsync(input, DefaultOptions);
                }

                await message.ModifyAsync(game.GetMessageUpdate(), requestOptions); // Restore display to normal
            }
            catch (HttpException) { } // Message is deleted while controls are added
            catch (TaskCanceledException) { } // Message is edited before the post-controls edit
        }
    }
}
