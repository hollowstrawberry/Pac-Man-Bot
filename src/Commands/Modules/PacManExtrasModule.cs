using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using PacManBot.Constants;
using PacManBot.Extensions;
using PacManBot.Games.Concrete;
using PacManBot.Utils;

namespace PacManBot.Commands.Modules
{
    [Name(ModuleNames.Pacman), Remarks("4")]
    public class PacManExtrasModule : BaseGameModule<PacManGame>
    {
        private const int MaxDisplayedScores = 20;

        
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
                await ReplyAsync($"Invalid range of scores. Try `{Context.Prefix}help lb` for more info");
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
            content.Append($"Displaying best scores {period.Humanized()}\n{Empty}\n");

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
                    Description = $"Highest score {time.Humanized()}:\n" + scores.First().ToString(Context.Client),
                    Color = Colors.PacManYellow
                };

                await ReplyAsync(embed);
                return;
            }

            await ReplyAsync(time == TimePeriod.All ? "No scores registered for this user!" : "No scores registered during that time!");
        }


        [Command("changedisplay"), Alias("display"), Remarks("Switch between normal and slim display")]
        [Summary("A Pac-Man game can either be in normal or slim mode. Slim mode fits better on phones." +
                 "Using this command will switch modes for the current game in this channel.")]
        public async Task ChangeGameDisplay()
        {
            if (Game == null)
            {
                await ReplyAsync("There is no active Pac-Man game in this channel!");
                return;
            }

            Game.slimDisplay = !Game.slimDisplay;
            await UpdateGameMessageAsync();

            await AutoReactAsync();
        }
    }
}
