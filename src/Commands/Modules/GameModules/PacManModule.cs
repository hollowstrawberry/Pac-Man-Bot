using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.Exceptions;
using PacManBot.Constants;
using PacManBot.Extensions;
using PacManBot.Games;
using PacManBot.Games.Concrete;

namespace PacManBot.Commands.Modules
{
    [Description(ModuleNames.Games)]
    public class PacManModule : BaseGameModule<PacManGame>
    {
        [Command("pacman"), Priority(1)]
        [Description(
            "Starts a new Pac-Man game in this channel.\nYou can add \"slim\" or \"s\" " +
            "after the command to use **Slim Mode**, which fits better on phones. If slim mode is still too wide, " +
            "you could reduce the font size in your phone's settings, if you want to." +
            "Use **{prefix}display** to change display modes later.\n" +
            "You can also play a custom Pac-Man map with the command **{prefix}custompacman**\n\n" +
            "Use **{prefix}bump** to move the game message to the bottom of the chat. Use **{prefix}cancel** to end the game. ")]
        public async Task StartGame(CommandContext ctx, string arg = "")
        {
            if (await CheckGameAlreadyExistsAsync(ctx)) return;

            arg = arg.ToLowerInvariant();
            bool mobile = arg.StartsWith("s") || arg.StartsWith("m");

            StartNewGame(new PacManGame(ctx.Channel.Id, ctx.User.Id, null, mobile, Services));
            var msg = await RespondGameAsync(ctx, await Game(ctx).GetContentAsync(showHelp: false) + "```diff\n+Starting game```");
            await AddControls(Game(ctx), msg);
        }


        [Command("custompacman"), Priority(0)]
        [Description(
            "Starts a new Pac-Man game in this channel using the provided custom map.\n" +
            "Use **{prefix}custompacman** by itself to see a guide for custom maps.\n\n" +
            "Use **{prefix}display** to switch between normal mode and slim mode. " +
            "Use **{prefix}bump** to move the game message to the bottom of the chat. Use **{prefix}cancel** to end the game.")]
        public async Task StartCustomGame(CommandContext ctx, [RemainingText]string map = null)
        {
            if (map == null)
            {
                string message = Content.customHelp.Replace("{prefix}", ctx.Prefix);

                var embed = new DiscordEmbedBuilder { Color = Colors.PacManYellow };
                foreach (var (name, url) in Content.customLinks)
                {
                    embed.AddField(name, $"[Click here]({url} \"{url}\")", true);
                }

                await ctx.RespondAsync(message, embed);
                return;
            }

            if (await CheckGameAlreadyExistsAsync(ctx)) return;

            map = map.ExtractCode();

            PacManGame newGame;
            try
            {
                newGame = new PacManGame(ctx.Channel.Id, ctx.User.Id, map, false, Services);
            }
            catch (InvalidMapException e)
            {
                await ctx.RespondAsync(
                    $"That's not a valid map!: {e.Message}.\n" +
                    $"Use `{Storage.GetPrefix(ctx)}custompacman` by itself for a guide on custom maps.");
                return;
            }

            StartNewGame(newGame);

            var msg = await RespondGameAsync(ctx, await Game(ctx).GetContentAsync(showHelp: false) + "```diff\n+Starting game```");
            await AddControls(Game(ctx), msg);
        }


        [Command("changedisplay"), Aliases("display"), Hidden]
        [Description("A Pac-Man game can either be in normal or slim mode. Slim mode fits better on phones." +
                 "Using this command will switch modes for the current game in this channel.")]
        public async Task ChangeGameDisplay(CommandContext ctx)
        {
            if (Game(ctx) == null)
            {
                await ctx.RespondAsync("There is no active Pac-Man game in this channel!");
                return;
            }

            Game(ctx).slimDisplay = !Game(ctx).slimDisplay;
            await UpdateGameMessageAsync(ctx);

            await ctx.AutoReactAsync();
        }




        public static async Task AddControls(PacManGame game, DiscordMessage message)
        {
            try
            {
                foreach (var input in PacManGame.GameInputs.Keys)
                {
                    if (game.State != GameState.Active) break;
                    await message.CreateReactionAsync(input);
                }

                await message.ModifyWithGameAsync(game);
            }
            catch (NotFoundException) { }
        }
    }
}
