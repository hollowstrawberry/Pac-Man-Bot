using System;
using System.Linq;
using System.Threading.Tasks;
using Discord.Commands;
using Discord.Net;
using PacManBot.Extensions;
using PacManBot.Games;
using PacManBot.Games.Concrete;

namespace PacManBot.Commands.Modules.GameModules
{
    [Name("👾More Games"), Remarks("3")]
    public class HangmanModule : BaseGameModule<HangmanGame>
    {
        [Command("hangman"), Alias("hang")]
        [Remarks("Start a game of Hangman in a channel")]
        [Summary("When this command is used, the channel becomes a public game of hangman.\n" +
                 "Anyone will be able to guess either a letter or the full word. Up to 10 wrong guesses!\n\n" +
                 "You can use **{prefix}hangman choose** if you want to choose a word or phrase for your friends to guess. " +
                 "Don't send it in the chat! The bot will ask in private.")]
        public async Task StartHangman([Remainder]string args = null)
        {
            if (await CheckGameAlreadyExistsAsync()) return;

            if (args != null)
            {
                await ReplyAsync(
                    $"You can use `{Context.Prefix}hangman choose` if you want to choose what the rest will have to guess.\n" +
                    $"The bot will ask you in private!");
                return;
            }

            StartNewGame(new HangmanGame(Context.Channel.Id, Services));
            await ReplyGameAsync();
        }


        [Command("hangman choose"), Alias("hang choose", "hangman word", "hang word"), Priority(1), HideHelp]
        [Summary("When this command is used, you will be sent a DM asking for a word or phrase in private. " +
                 "Once you give it, the game will start in the original channel where you used this command.\n" +
                 "Anyone will be able to guess either a letter or the full phrase. Up to 10 wrong guesses!\n\n" +
                 "To start a normal game with a random word, use **{prefix}hangman**")]
        public async Task StartHangmanCustom()
        {
            if (Context.Guild == null)
            {
                await ReplyAsync($"There's nobody here to guess! To play alone, use `hangman`");
                return;
            }

            if (await CheckGameAlreadyExistsAsync()) return;


            StartNewGame(new HangmanGame(Context.Channel.Id, Context.User.Id, Services));

            var userChannel = await Context.User.GetOrCreateDMChannelAsync();
            try
            {
                await userChannel.SendMessageAsync(
                    $"Send the secret word or phrase for the {Game.GameName} game in {Context.Channel.Mention()}:");
            }
            catch (HttpException e) when (e.DiscordCode == 50007) // Can't send DMs
            {
                await ReplyAsync($"{Context.User.Mention} You must enable DMs!");
                EndGame();
                return;
            }

            var msg = await ReplyGameAsync($"{Context.User.Mention} check your DMs!");

            while (true)
            {
                var response = await Input.GetResponse(x =>
                    x.Channel.Id == userChannel.Id && x.Author.Id == Context.User.Id, 90);

                if (response == null)
                {
                    EndGame();
                    await userChannel.SendMessageAsync("Timed out 💨");
                    await UpdateGameMessageAsync();
                }

                string word = response.Content.ToUpperInvariant().Replace('\n', ' ');
                string wf = response.Content.Contains(' ') ? "phrase" : "word";

                if (!HangmanGame.Alphabet.IsMatch(word))
                {
                    await userChannel.SendMessageAsync(
                        $"Sorry, but your secret {wf} can only contain alphabet characters (A-Z).\nTry again.");
                }
                else if (word.Length > 40)
                {
                    await userChannel.SendMessageAsync(
                        $"Sorry, but your secret {wf} can only be up to 40 characters long.\nTry again.");
                }
                else if (word.Count(x => x == ' ') > 5)
                {
                    await userChannel.SendMessageAsync(
                        $"Sorry, but your secret phrase can only be up to six words long.\nTry again.");
                }
                else
                {
                    Game.SetWord(word);
                    await response.AutoReactAsync();
                    await SendOrUpdateGameMessageAsync();

                    return;
                }
            }
        }
    }
}
