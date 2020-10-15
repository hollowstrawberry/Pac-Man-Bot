using System.Linq;
using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Exceptions;
using PacManBot.Extensions;
using PacManBot.Games.Concrete;

namespace PacManBot.Commands.Modules
{
    [Category(Categories.Games)]
    [RequireBotPermissions(BaseBotPermissions)]
    public class HangmanModule : BaseGameModule<HangmanGame>
    {
        [Command("hangman"), Aliases("hang"), Priority(2)]
        [Description(
            "When this command is used, the channel becomes a public game of hangman.\n" +
            "Anyone will be able to guess either a letter or the full word. Up to 10 wrong guesses!\n\n" +
            "You can use `hangmanword` if you want to choose a word or phrase for your friends to guess. " +
            "Don't send it in the chat! The bot will ask in private.")]
        public async Task StartHangman(CommandContext ctx, [RemainingText]string args = null)
        {
            if (await CheckGameAlreadyExistsAsync(ctx)) return;

            if (args != null)
            {
                await ctx.RespondAsync(
                    $"You can use `{Storage.GetPrefix(ctx)}hangmanword` if you want to choose what the rest will have to guess.\n" +
                    $"The bot will ask you in private!");
                return;
            }

            StartNewGame(new HangmanGame(ctx.Channel.Id, Services));
            await RespondGameAsync(ctx);
        }


        [Command("hangmanword"), Aliases("hangchoose", "hangmanchoose", "hangword")]
        [Priority(10), Hidden]
        [Description(
            "When this command is used, you will be sent a DM asking for a word or phrase in private. " +
            "Once you give it, the game will start in the original channel where you used this command.\n" +
            "Anyone will be able to guess either a letter or the full phrase. Up to 10 wrong guesses!\n\n" +
            "To start a normal game with a random word, use `hangman`")]
        [RequireGuild]
        public async Task StartHangmanCustom(CommandContext ctx)
        {
            if (ctx.Guild == null)
            {
                await ctx.RespondAsync($"There's nobody here to guess! To play alone, use `hangman`");
                return;
            }

            if (await CheckGameAlreadyExistsAsync(ctx)) return;


            StartNewGame(new HangmanGame(ctx.Channel.Id, ctx.User.Id, Services));

            var dm = await ctx.Member.CreateDmChannelAsync();
            try
            {
                await dm.SendMessageAsync(
                    $"Send the secret word or phrase for the {Game(ctx).GameName} game in {ctx.Channel.Mention}:");
            }
            catch (UnauthorizedException)
            {
                await ctx.RespondAsync($"{ctx.User.Mention} You must enable DMs!");
                EndGame(ctx);
                return;
            }

            var msg = await RespondGameAsync(ctx, $"{ctx.User.Mention} check your DMs!");

            while (true)
            {
                var response = await Input.GetResponseAsync(x =>
                    x.Channel.Id == dm.Id && x.Author.Id == ctx.User.Id, 90);

                if (response == null)
                {
                    EndGame(ctx);
                    await dm.SendMessageAsync("Timed out 💨");
                    await UpdateGameMessageAsync(ctx);
                    return;
                }

                string word = response.Content.ToUpperInvariant().Replace('\n', ' ');
                string wf = response.Content.Contains(' ') ? "phrase" : "word";

                if (!HangmanGame.Alphabet.IsMatch(word))
                {
                    await dm.SendMessageAsync(
                        $"Sorry, but your secret {wf} can only contain alphabet characters (A-Z).\nTry again.");
                }
                else if (word.Length > 40)
                {
                    await dm.SendMessageAsync(
                        $"Sorry, but your secret {wf} can only be up to 40 characters long.\nTry again.");
                }
                else if (word.Count(x => x == ' ') > 5)
                {
                    await dm.SendMessageAsync(
                        $"Sorry, but your secret phrase can only be up to six words long.\nTry again.");
                }
                else
                {
                    Game(ctx).SetWord(word);
                    await response.AutoReactAsync();
                    await SendOrUpdateGameMessageAsync(ctx);
                    return;
                }
            }
        }
    }
}
