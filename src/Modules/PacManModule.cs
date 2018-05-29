using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Net;
using Discord.Commands;
using Discord.WebSocket;
using PacManBot.Games;
using PacManBot.Services;
using PacManBot.Constants;
using static PacManBot.Games.GameUtils;

namespace PacManBot.Modules
{
    [Name("🎮Pac-Man"), Remarks("1")]
    public class PacManModule : ModuleBase<SocketCommandContext>
    {
        private readonly DiscordShardedClient shardedClient;
        private readonly LoggingService logger;
        private readonly StorageService storage;

        private const int MaxDisplayedScores = 20;


        public PacManModule(DiscordShardedClient shardedClient, LoggingService logger, StorageService storage)
        {
            this.shardedClient = shardedClient;
            this.logger = logger;
            this.storage = storage;
        }



        [Command("play"), Alias("pacman", "p", "start"), Remarks("Start a new game in this channel"), Parameters("[mobile] [\\`\\`\\`custom map\\`\\`\\`]")]
        [Summary("Starts a new Pac-Man game, unless there is already a game in this channel.\nAdding \"mobile\" or \"m\" after the command will begin the game in *Mobile Mode*, "
               + "which uses simple characters that will work in phones. Use **{prefix}display** to change mode later.\n\nIf you add a valid customized map "
               + "between \\`\\`\\`triple backticks\\`\\`\\`, it will start a custom game using that map instead. For more information about custom games, use the **{prefix}custom** command.\n\n"
               + "Use **{prefix}bump** to move the game message to the bottom of the chat. Use **{prefix}cancel** to end the game. ")]
        [BetterRequireBotPermission(ChannelPermission.ReadMessageHistory | ChannelPermission.UseExternalEmojis | ChannelPermission.AddReactions | ChannelPermission.EmbedLinks)]
        public async Task StartGameInstance([Remainder]string args = "")
        {
            if (!Context.BotCan(ChannelPermission.SendMessages)) return;

            string prefix = storage.GetPrefixOrEmpty(Context.Guild);

            foreach (var game in storage.Games)
            {
                if (Context.Channel.Id == game.ChannelId)
                {
                    await ReplyAsync(game is PacManGame ?
                        $"There is already a game in this channel!\nYou can use the **{prefix}bump** command to bring it to the bottom of the chat." :
                        $"There is already a different game in this channel! Try using **{prefix}cancel**", options: Utils.DefaultOptions);
                    return;
                }
            }

            string[] argSplice = args.Split("```");
            string preMessage = "";

            bool mobile = false;
            if (argSplice[0].StartsWith("m")) mobile = true;
            else if (!string.IsNullOrWhiteSpace(argSplice[0])) preMessage = $"Unknown game argument \"{argSplice[0]}\".";

            string customMap = null;
            if (args.Contains("```")) customMap = argSplice[1].Trim('\n', '`');

            PacManGame newGame;
            try
            {
                newGame = new PacManGame(Context.Channel.Id, Context.User.Id, customMap, mobile, shardedClient, logger, storage);
            }
            catch (InvalidMapException e) when (customMap != null)
            {
                await logger.Log(LogSeverity.Debug, LogSource.Game, $"Failed to create custom game: {e.Message}");
                await ReplyAsync($"The provided map is invalid: {e.Message}.\nUse the **{prefix}custom** command for more info.", options: Utils.DefaultOptions);
                return;
            }
            catch (Exception e)
            {
                await logger.Log(LogSeverity.Error, LogSource.Game, $"{e}");
                await ReplyAsync($"There was an error starting the game. Please try again or contact the author of the bot using **{prefix}feedback**", options: Utils.DefaultOptions);
                return;
            }

            storage.AddGame(newGame);

            var gameMessage = await ReplyAsync(preMessage + newGame.GetContent(showHelp: false) + "```diff\n+Starting game```", options: Utils.DefaultOptions); //Output the game
            newGame.MessageId = gameMessage.Id;

            await AddControls(newGame, gameMessage);
        }


        [Command("changedisplay"), Alias("display"), Remarks("Switch between normal and mobile display")]
        [Summary("A Pac-Man game can either be in normal or mobile mode. Using this command will switch modes for the current game in this channel")]
        public async Task ChangeGameDisplay()
        {
            var game = storage.GetGame<PacManGame>(Context.Channel.Id);
            if (game == null)
            {
                await ReplyAsync("There is no active Pac-Man game in this channel!", options: Utils.DefaultOptions);
                return;
            }

            game.mobileDisplay = !game.mobileDisplay;
            try
            {
                game.CancelRequests();
                var gameMessage = await game.GetMessage();
                if (gameMessage != null) await gameMessage.ModifyAsync(game.UpdateMessage, game.RequestOptions);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                if (!(e is HttpException || e is TimeoutException)) await logger.Log(LogSeverity.Error, $"{e}");
                await Context.Message.AddReactionAsync(CustomEmoji.Cross, Utils.DefaultOptions);
                return;
            }

            await Context.Message.AddReactionAsync(CustomEmoji.Check, Utils.DefaultOptions);
        }


        [Command("leaderboard"), Alias("lb", "l"), Parameters("[period] [start] [end]")]
        [Remarks("Global Leaderboard scores"), ExampleUsage("leaderboard 5\nlb month 11 30")]
        [Summary("By default, displays the top 10 Pac-Man scores of all time from the Global Leaderboard of all servers.\n"
               + "You can specify the [period] to display scores from: all/month/week/day (a/m/w/d are also valid). "
               + "You can also specify a range of scores from [start] to [end], where those are two positive numbers.\n"
               + "Only 20 scores may be displayed at once. If given just one number, it will be taken as the start if it's above 20, or as the end otherwise.")]
        public async Task SendTopScores(int min = 10, int? max = null) => await SendTopScores(Utils.TimePeriod.all, min, max);

        [Command("leaderboard"), Alias("lb", "l")]
        public async Task SendTopScores(Utils.TimePeriod time, int min = 10, int? max = null)
        {
            if (min < 1 || max < 1 || max < min)
            {
                await ReplyAsync($"Invalid range of scores. Try **{storage.GetPrefixOrEmpty(Context.Guild)}help lb** for more info", options: Utils.DefaultOptions);
                return;
            }
            if (max == null) 
            {
                if (min <= MaxDisplayedScores) //Takes a single number as the max if less than the limit
                {
                    max = min;
                    min = 1;
                }
                else max = min + 9;
            }

            var scores = storage.GetScores(time);

            if (scores.Count < 1)
            {
                await ReplyAsync("There are no registered scores within this period!", options: Utils.DefaultOptions);
                return;
            }

            if (min > scores.Count)
            {
                await ReplyAsync("No scores found within the specified range.", options: Utils.DefaultOptions);
                return;
            }

            var content = new StringBuilder();
            content.Append($"Displaying best scores {Utils.ScorePeriodString(time)}\nᅠ\n");

            for (int i = min; i < scores.Count() && i <= max && i < min + MaxDisplayedScores; i++)
            {
                ScoreEntry entry = scores[i - 1]; // The list is always kept sorted so we just go by index

                //Align each element in the line
                string result = $"`{$"{i}.".AlignTo($"{min + MaxDisplayedScores}.")} {$"({entry.state})".Align(6)} {entry.score.AlignTo(scores[min - 1].score, right: true)} points in {entry.turns} turns";
                content.Append(result.Align(38) + $"- {entry.GetUsername(shardedClient).Replace("`", "")}`\n");
            }

            content.Append(max - min >= MaxDisplayedScores && max < scores.Count ? $"*Only {MaxDisplayedScores} scores may be displayed at once*"
                           : max >= scores.Count ? "*No more scores could be found*" : "");


            var embed = new EmbedBuilder()
            {
                Title = $"🏆 __**Pac-Man Global Leaderboard**__ 🏆",
                Description = content.ToString(),
                Color = new Color(241, 195, 15)
            };

            await ReplyAsync("", false, embed.Build(), Utils.DefaultOptions);
        }


        [Command("score"), Alias("sc", "s"), Parameters("[period] [user]")]
        [ExampleUsage("score d\nsc week @Samrux#3980"), Remarks("See your or a person's top score")]
        [Summary("See your highest Pac-Man score of all time in the *Global Leaderboard* of all servers. You can specify a user in your guild using their name, mention or ID, to see their score instead."
               + "\nYou can also specify a time period to display scores from: all/month/week/day (a/m/w/d are also valid)")]
        public async Task SendPersonalBest(SocketGuildUser guildUser = null) => await SendPersonalBest(Utils.TimePeriod.all, (guildUser ?? Context.User).Id);

        [Command("score"), Alias("sc", "s")]
        public async Task SendPersonalBest(Utils.TimePeriod time, SocketGuildUser guildUser = null) => await SendPersonalBest(time, (guildUser ?? Context.User).Id);

        [Command("score"), Alias("sc", "s")]
        public async Task SendPersonalBest(ulong userId) => await SendPersonalBest(Utils.TimePeriod.all, userId);

        [Command("score"), Alias("sc", "s")]
        public async Task SendPersonalBest(Utils.TimePeriod time, ulong userId)
        {
            var scores = storage.GetScores(time);
            for (int i = 0; i < scores.Count; i++)
            {
                if (scores[i].userId == userId) // The list is always kept sorted so the first match is the highest score
                {
                    var embed = new EmbedBuilder()
                    {
                        Title = $"🏆 __**Pac-Man Global Leaderboard**__ 🏆",
                        Description = $"Highest score {Utils.ScorePeriodString(time)}:\n{scores[i].ToStringSimpleScoreboard(shardedClient, i + 1)}",
                        Color = new Color(241, 195, 15)
                    };

                    await ReplyAsync("", false, embed.Build(), Utils.DefaultOptions);
                    return;
                }
            }

            await ReplyAsync(time == Utils.TimePeriod.all ? "No scores registered for this user!" : "No scores registered during that time!", options: Utils.DefaultOptions);
        }


        [Command("custom"), Remarks("Learn how custom maps work")]
        [Summary("Using this command will display detailed help about the custom Pac-Man maps that you can design and play yourself!")]
        [BetterRequireBotPermission(ChannelPermission.EmbedLinks)]
        public async Task SayCustomMapHelp()
        {
            string message = storage.BotContent["customhelp"].Replace("{prefix}", storage.GetPrefixOrEmpty(Context.Guild));
            string[] links = storage.BotContent["customlinks"].Split('\n').Where(s => s.Contains("|")).ToArray();

            var embed = new EmbedBuilder() { Color = new Color(241, 195, 15) };
            for (int i = 0; i < links.Length; i++)
            {
                embed.AddField(links[i].Split('|')[0], $"[Click here]({links[i].Split('|')[1]} \"{links[i].Split('|')[1]}\")", true);
            }

            await ReplyAsync(message, false, embed.Build(), Utils.DefaultOptions);
        }


        public static async Task AddControls(PacManGame game, IUserMessage message)
        {
            try
            {
                var requestOptions = game.RequestOptions; // So the edit can be cancelled

                foreach (IEmote input in PacManGame.GameInputs.Keys)
                {
                    if (game.State != State.Active) break;
                    await message.AddReactionAsync(input, Utils.DefaultOptions);
                }

                await message.ModifyAsync(game.UpdateMessage, requestOptions); //Restore display to normal
            }
            catch (Exception e) when (e is HttpException || e is TimeoutException || e is OperationCanceledException) { } // We can ignore these
        }
    }
}
