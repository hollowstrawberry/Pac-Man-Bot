using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Net;
using Discord.Commands;
using Discord.WebSocket;
using PacManBot.Utils;
using PacManBot.Games;
using PacManBot.Games.Concrete;
using PacManBot.Constants;
using PacManBot.Extensions;

namespace PacManBot.Commands.Modules
{
    [Name("🎮Pac-Man"), Remarks("2")]
    [BetterRequireBotPermission(ChannelPermission.ReadMessageHistory | ChannelPermission.EmbedLinks |
                                ChannelPermission.UseExternalEmojis | ChannelPermission.AddReactions)]
    public class PacManModule : BaseCustomModule
    {
        public PacManModule(IServiceProvider services) : base(services) { }


        private const int MaxDisplayedScores = 20;


        [Command("pacman"), Alias("p", "start"), Remarks("Start a new game in this channel"), Parameters("[mobile] [map]")]
        [Summary("Starts a new Pac-Man game, unless there is already a game in this channel.\nAdding \"mobile\" or \"m\" " +
                 "after the command will begin the game in *Mobile Mode*, which uses simple characters that will work in phones. " +
                 "Use **{prefix}display** to change mode later.\n\nIf you add a valid customized map between \\`\\`\\`triple backticks\\`\\`\\`, " +
                 "it will start a custom game using that map instead. For more information about custom games, use the **{prefix}custom** command.\n\n" +
                 "Use **{prefix}bump** to move the game message to the bottom of the chat. Use **{prefix}cancel** to end the game. ")]
        public async Task StartGameInstance([Remainder]string args = "")
        {
            if (!Context.BotCan(ChannelPermission.SendMessages)) return;

            var existingGame = Games.GetForChannel(Context.Channel.Id);
            if (existingGame != null)
            {
                await ReplyAsync(existingGame.UserId.Contains(Context.User.Id) ?
                    $"You're already playing a game in this channel!\nUse `{Prefix}cancel` if you want to cancel it or `{Prefix}bump` to bring it to the bottom." :
                    $"There is already a different game in this channel!\nWait until it's finished or try doing `{Prefix}cancel`");
                return;
            }

            string[] argSplice = args.Split("```");
            string preMessage = "";

            bool mobile = false;
            if (argSplice[0].StartsWith("m")) mobile = true;
            else if (!string.IsNullOrWhiteSpace(argSplice[0])) preMessage = $"Unknown game argument \"{argSplice[0]}\".";

            string customMap = null;
            if (args.Contains("```")) customMap = argSplice[1].Trim('\n', '`').ReplaceMany((".", "·"), ("o", "●"));

            PacManGame newGame;
            try
            {
                newGame = new PacManGame(Context.Channel.Id, Context.User.Id, customMap, mobile, Services);
            }
            catch (InvalidMapException e) when (customMap != null)
            {
                await Logger.Log(LogSeverity.Debug, LogSource.Game, $"Failed to create custom game: {e.Message}");
                await ReplyAsync($"The provided map is invalid: {e.Message}.\nUse the `{Prefix}custom` command for more info.");
                return;
            }
            catch (Exception e)
            {
                await Logger.Log(LogSeverity.Error, LogSource.Game, $"{e}");
                await ReplyAsync("There was an error starting the game. " +
                                 $"Please try again or contact the author of the bot using `{Prefix}feedback`");
                return;
            }

            Games.Add(newGame);

            var gameMessage = await ReplyAsync(preMessage + newGame.GetContent(showHelp: false) + "```diff\n+Starting game```");
            newGame.MessageId = gameMessage.Id;

            await AddControls(newGame, gameMessage);
        }



        [Command("changedisplay"), Alias("display"), Remarks("Switch between normal and mobile display")]
        [Summary("A Pac-Man game can either be in normal or mobile mode. Using this command will switch modes " +
                 "for the current game in this channel")]
        public async Task ChangeGameDisplay()
        {
            var game = Games.GetForChannel<PacManGame>(Context.Channel.Id);
            if (game == null)
            {
                await ReplyAsync("There is no active Pac-Man game in this channel!");
                return;
            }

            game.mobileDisplay = !game.mobileDisplay;
            try
            {
                game.CancelRequests();
                var gameMessage = await game.GetMessage();
                if (gameMessage != null) await gameMessage.ModifyAsync(game.GetMessageUpdate(), game.GetRequestOptions());
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                if (!(e is HttpException || e is TimeoutException)) await Logger.Log(LogSeverity.Error, $"{e}");
                await AutoReactAsync(false);
                return;
            }

            await AutoReactAsync();
        }



        [Command("top"), Alias("leaderboard", "lb"), Parameters("[period] [range]")]
        [Remarks("Global Leaderboard scores"), ExampleUsage("top 5\ntop month 11 30")]
        [Summary("By default, displays the top 10 Pac-Man scores of all time from the Global Leaderboard of all servers.\n"
               + "You can specify the [period] to display scores from: all/month/week/day (a/m/w/d are also valid). "
               + "You can also specify a [range] of scores with two positive whole numbers (start and end).\n"
               + "Only 20 scores may be displayed at once.")]
        public async Task SendTopScores(int min = 10, int? max = null)
            => await SendTopScores(TimePeriod.All, min, max);


        [Command("top"), Alias("leaderboard", "lb"), HideHelp]
        public async Task SendTopScores(TimePeriod period, int min = 10, int? max = null)
        {
            if (max == null) 
            {
                if (min <= MaxDisplayedScores) // Takes a single number as the max if less than the limit
                {
                    max = min;
                    min = 1;
                }
                else max = min + 9;
            }
            if (min < 1 || max < 1 || max < min)
            {
                await ReplyAsync($"Invalid range of scores. Try `{Prefix}help lb` for more info");
                return;
            }

            int amount = Math.Min(MaxDisplayedScores, (int)max - min + 1);

            var scores = Storage.GetScores(period, min - 1, amount);

            if (scores.Count == 0)
            {
                await ReplyAsync($"No scores found in this range{" and period".If(period != TimePeriod.All)}.");
                return;
            }

            var content = new StringBuilder();
            content.Append($"Displaying best scores {period.Humanized()}\nᅠ\n");

            int maxPosDigits = max.ToString().Length;
            int maxScoreDigits = scores[0].Score.ToString().Length;
            for (int i = 0; i < scores.Count; i++)
            {
                var entry = scores[i];

                string result = $"`{$"{min + i}.".PadRight(maxPosDigits+1)} {$"({entry.State})".PadRight(6)} " +
                                $"{$"{entry.Score}".PadLeft(maxScoreDigits)} points in {entry.Turns} turns";
                content.AppendLine(result.PadRight(38) + $"- {entry.GetUsername(Context.Client).Replace("`", "")}`");
            }

            if (max - min + 1 > MaxDisplayedScores)
            {
                content.AppendLine($"*Only {MaxDisplayedScores} scores may be displayed at once*");
            }
            if (scores.Count < amount)
            {
                content.AppendLine("*No more scores could be found*");
            }


            var embed = new EmbedBuilder()
            {
                Title = "🏆 __**Pac-Man Global Leaderboard**__ 🏆",
                Description = content.ToString().Truncate(2047),
                Color = Colors.PacManYellow
            };

            await ReplyAsync(embed);
        }


        [Command("score"), Alias("sc", "s"), Parameters("[period] [user]")]
        [ExampleUsage("score d\nsc week @Samrux#3980"), Remarks("See your or a person's top score")]
        [Summary("See your highest Pac-Man score of all time in the *Global Leaderboard* of all servers. " +
                 "You can specify a user in your guild using their name, mention or ID, to see their score instead." +
                 "\nYou can also specify a time period to display scores from: all/month/week/day (a/m/w/d are also valid)")]
        public async Task SendPersonalBest(SocketGuildUser guildUser = null)
            => await SendPersonalBest(TimePeriod.All, (guildUser ?? Context.User).Id);

        [Command("score"), Alias("sc", "s"), HideHelp]
        public async Task SendPersonalBest(TimePeriod time, SocketGuildUser guildUser = null)
            => await SendPersonalBest(time, (guildUser ?? Context.User).Id);

        [Command("score"), Alias("sc", "s"), HideHelp]
        public async Task SendPersonalBest(ulong userId)
            => await SendPersonalBest(TimePeriod.All, userId);

        [Command("score"), Alias("sc", "s"), HideHelp]
        public async Task SendPersonalBest(TimePeriod time, ulong userId)
        {
            var scores = Storage.GetScores(time, 0, 1, userId);

            if (scores.Count > 0)
            {
                var embed = new EmbedBuilder
                {
                    Title = "🏆 __**Pac-Man Global Leaderboard**__ 🏆",
                    Description = $"Highest score {time.Humanized()}:\n" +
                                    scores.First().ToString(Context.Client),
                    Color = Colors.PacManYellow
                };

                await ReplyAsync(embed);
                return;
            }

            await ReplyAsync(time == TimePeriod.All ? "No scores registered for this user!" : "No scores registered during that time!");
        }


        [Command("custom"), Remarks("Learn how custom maps work")]
        [Summary("Using this command will display detailed help about the custom Pac-Man maps that you can design and play yourself!")]
        public async Task SayCustomMapHelp()
        {
            string message = Content.customHelp.Replace("{prefix}", Prefix);

            var embed = new EmbedBuilder { Color = Colors.PacManYellow };
            foreach (var (name, url) in Content.customLinks)
            {
                embed.AddField(name, $"[Click here]({url} \"{url}\")", true);
            }

            await ReplyAsync(message, embed);
        }




        public static async Task AddControls(PacManGame game, IUserMessage message)
        {
            try
            {
                var requestOptions = game.GetRequestOptions(); // So the edit can be cancelled

                foreach (var input in PacManGame.GameInputs.Keys)
                {
                    if (game.State != State.Active) break;
                    await message.AddReactionAsync(input, DefaultOptions);
                }

                await message.ModifyAsync(game.GetMessageUpdate(), requestOptions); // Restore display to normal
            }
            catch (Exception e) when (e is HttpException || e is TimeoutException || e is OperationCanceledException) { } // Ignore
        }
    }
}
