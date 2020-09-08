using System.Threading.Tasks;
using Discord.Commands;
using PacManBot.Games.Concrete;

namespace PacManBot.Commands.Modules.GameModules
{
    [Name(ModuleNames.Games), Remarks("3")]
    public class CodeBreakModule : BaseGameModule<CodeBreakGame>
    {
        [Command("codebreak"), Alias("code", "break"), Parameters("[digits]")]
        [Remarks("Start the code-breaking game"), Priority(1)]
        [Summary("When this command is used, the channel becomes a public game of Code Break.\n" +
                 "In Code Break, you must find the secret 4-digit code by making guesses, " +
                 "and using the two clues you get: how many of the digits are a match and " +
                 "how many of the digits are near (in the wrong position).\n" +
                 "The code can never have 2 or more of the same digit.\n" +
                 "You can also specify the amount of digits from 2 to 10.\n" +
                 "Fun fact: Any 4-digit code can be cracked in 7 guesses or less if played perfectly!")]
        public async Task StartHangman(int digits = 4)
        {
            if (await CheckGameAlreadyExistsAsync()) return;

            if (digits < 2 || digits > 10)
            {
                await ReplyAsync("The amount of digits must be between 2 and 10.");
                return;
            }

            StartNewGame(new CodeBreakGame(Context.Channel.Id, Context.User.Id, digits, Services));
            await ReplyGameAsync();
        }
    }
}
