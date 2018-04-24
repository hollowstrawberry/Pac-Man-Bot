using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using PacManBot.Services;
using PacManBot.Constants;
using static PacManBot.Modules.PacMan.GameInstance;
using System.Collections.Generic;
using System.Text;

namespace PacManBot.Modules.PacMan
{
    [Name("🎮Game")]
    public class PacManModule : ModuleBase<SocketCommandContext>
    {
        private readonly LoggingService logger;
        private readonly StorageService storage;

        private const int MaxDisplayedScores = 20;
        private string ManualModeMessage => "__Manual mode:__ Both adding and removing reactions count as input. Do one action at a time to prevent buggy behavior." + "\nGive this bot the permission to Manage Messages to remove reactions automatically.".If(Context.Guild != null);

        public enum TimePeriod // Argument to leaderboard command, parsable from string
        {
            all, month, week, day,
            a = all, m = month, w = week, d = day
        }


        public PacManModule(LoggingService logger, StorageService storage)
        {
            this.logger = logger;
            this.storage = storage;
        }



        [Command("play"), Alias("p"), Remarks("[mobile/m] [\\`\\`\\`custom map\\`\\`\\`] — *Start a new game on this channel*")]
        [Summary("Starts a new game, unless there is already an active game on this channel.\nAdding \"mobile\" or \"m\" after the command will begin the game in *Mobile Mode*, which uses simple characters that will work in phones. (To change back to normal mode, use the **{prefix}refresh** command.)\nIf you add a valid customized map between \\`\\`\\`triple backticks\\`\\`\\`, it will start a custom game using that map instead. For more information about custom games, use the **{prefix}custom** command.")]
        [RequireBotPermission(ChannelPermission.ReadMessageHistory | ChannelPermission.AddReactions)]
        public async Task StartGameInstance([Remainder]string args = "")
        {
            if (Context.Guild != null && !Context.BotHas(ChannelPermission.SendMessages)) return;

            string prefix = storage.GetPrefixOrEmpty(Context.Guild);

            foreach (GameInstance game in storage.GameInstances)
            {
                if (Context.Channel.Id == game.channelId) //Finds a game instance corresponding to this channel
                {
                    await ReplyAsync($"There is already an ongoing game on this channel!\nYou could use the **{prefix}refresh** command to bring it to the bottom of the chat.");
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

            GameInstance newGame;
            try
            {
                newGame = new GameInstance(Context.Channel.Id, Context.User.Id, customMap, Context.Client, storage, logger);
            }
            catch (InvalidMapException e)
            {
                if (customMap == null) await logger.Log(LogSeverity.Error, LogSource.Game, $"{e}");
                else await logger.Log(LogSeverity.Debug, LogSource.Game, $"Failed to create custom game: {e.Message}");
                await ReplyAsync($"The provided map is invalid: {e.Message}.{$"\nUse the **{prefix}custom** command for more info.".If(customMap != null)}");
                return;
            }
            catch (Exception e)
            {
                await logger.Log(LogSeverity.Error, LogSource.Game, $"{e}");
                await ReplyAsync($"There was an error starting the game. Please try again or contact the author of the bot using **{prefix}feedback**");
                return;
            }

            storage.GameInstances.Add(newGame);
 
            if (mobile) newGame.mobileDisplay = true;
            var gameMessage = await ReplyAsync(preMessage + newGame.GetDisplay() + "```diff\n+Starting game```"); //Output the game
            newGame.messageId = gameMessage.Id;

            if (!Context.BotHas(ChannelPermission.ManageMessages))
            {
                await ReplyAsync(ManualModeMessage);
            }

            try
            {
                await AddControls(gameMessage); //Controls for easy access
                await gameMessage.ModifyAsync(m => m.Content = newGame.GetDisplay()); //Restore display to normal
            }
            catch (Discord.Net.HttpException) {;} // Message not found (deleted at some point)
        }


        [Command("refresh"), Alias("r"), Remarks("[mobile/m] — *Move the game to the bottom of the chat*")]
        [Summary("If there is already an active game on this channel, using this command moves the game message to the bottom of the chat, and deletes the old one.\nThis is useful if the game message has been lost in a sea of other messages or if you encounter a problem with reactions.\nAdding \"mobile\" or \"m\" after the command will refresh the game in *Mobile Mode*, which uses simple characters that will work in phones. Refreshing again will return it to normal.")]
        [RequireBotPermission(ChannelPermission.ReadMessageHistory | ChannelPermission.AddReactions)]
        public async Task RefreshGameInstance(string arg = "")
        {
            foreach (GameInstance game in storage.GameInstances)
            {
                if (Context.Channel.Id == game.channelId) //Finds a game instance corresponding to this channel
                {
                    var oldMsg = await Context.Channel.GetMessageAsync(game.messageId);
                    if (oldMsg != null) await oldMsg.DeleteAsync(); //Delete old message

                    game.mobileDisplay = arg.StartsWith("m");
                    var newMsg = await ReplyAsync(game.GetDisplay() + "```diff\n+Refreshing game```"); //Send new message
                    game.messageId = newMsg.Id; //Change focus message for this channel

                    if (!Context.BotHas(ChannelPermission.ManageMessages))
                    {
                        await ReplyAsync(ManualModeMessage);
                    }

                    try
                    {
                        await AddControls(newMsg);
                        await newMsg.ModifyAsync(m => m.Content = game.GetDisplay()); //Edit message
                    }
                    catch (Discord.Net.HttpException) {;} // Not found (deleted at some point)

                    return;
                }
            }

            await ReplyAsync("There is no active game on this channel!");
        }


        [Command("end"), Alias("stop"), Remarks("— *End a game you started. Always usable by moderators*")]
        [Summary("Ends the current game on this channel, but only if the person using the command started the game or if they have the Manage Messages permission.")]
        public async Task EndGameInstance()
        {
            foreach (GameInstance game in storage.GameInstances)
            {
                if (Context.Channel.Id == game.channelId)
                {
                    if (game.ownerId == Context.User.Id || Context.Guild != null && Context.UserHas(ChannelPermission.ManageMessages))
                    {
                        storage.DeleteGame(game);
                        await ReplyAsync("Game ended.");

                        try
                        {
                            if (await Context.Channel.GetMessageAsync(game.messageId) is IUserMessage gameMessage)
                            {
                                if (Context.Guild != null) await gameMessage.DeleteAsync(); //So as to not leave spam in guild channels
                                else await gameMessage.ModifyAsync(m => m.Content = game.GetDisplay() + "```diff\n-Game has been ended!```"); //Edit message
                            }
                        }
                        catch (Discord.Net.HttpException e)
                        {
                            await logger.Log(LogSeverity.Warning, $"Failed to grab game message from removed game in {Context.Channel.Id}: {e.Message}");
                        }
                    }
                    else await ReplyAsync("You can't end this game because you didn't start it!");

                    return;
                }
            }

            await ReplyAsync("There is no active game on this channel!");
        }


        [Command("leaderboard"), Alias("l", "lb"), Remarks("[all/month/week/day] [number] [number] — *Displays scores from the Global Leaderboard, in a given range from a given period*")]
        [Summary("This command will display a list of scores in the *Global Leaderboard* of all servers.\nYou can specify a time period (all/month/day/week), as well as a start point and an end point for a range of scores to show. Only 20 scores may be displayed at once.")]
        public async Task SendTopScores(int min = 10, int max = -1) => await SendTopScores(TimePeriod.all, min, max);

        [Command("leaderboard"), Alias("l", "lb")]
        public async Task SendTopScores(TimePeriod time, int min = 10, int max = -1)
        {
            if (min < 1) min = 1; //Foolproofing
            if (max < 0 && min <= MaxDisplayedScores) //Takes the first number as the max
            {
                max = min;
                min = 1;
            }
            else if (max < min) max = min + 9;

            List<ScoreEntry> scores;
            var currentDate = DateTime.Now;
            switch (time)
            {
                case TimePeriod.day:   scores = storage.ScoreEntries.Where(s => (currentDate - s.date).TotalHours <= 24.0).ToList(); break;
                case TimePeriod.week:  scores = storage.ScoreEntries.Where(s => (currentDate - s.date).TotalDays <= 7.0).ToList(); break;
                case TimePeriod.month: scores = storage.ScoreEntries.Where(s => (currentDate - s.date).TotalDays <= 30.0).ToList(); break;
                case TimePeriod.all:   scores = storage.ScoreEntries; break;

                default:
                    await ReplyAsync("Unknown time period specified");
                    return;
            }

            int scoreAmount = scores.Count();

            if (scoreAmount < 1)
            {
                await ReplyAsync("There are no registered scores within this period!");
                return;
            }

            if (min > scoreAmount)
            {
                await ReplyAsync("No scores found within the specified range.");
                return;
            }

            var content = new StringBuilder();
            content.Append($"Displaying best scores {(time == TimePeriod.all ? "of all time" : time == TimePeriod.month ? "in the last 30 days" : time == TimePeriod.week ? "in the last 7 days" : $"in the last 24 hours")}\nᅠ\n");

            for (int i = min; i < scoreAmount && i <= max && i < min + MaxDisplayedScores; i++)
            {
                ScoreEntry entry = scores[i - 1]; // The list is always kept sorted so we just go by index

                // Fancy formatting
                string result = $"`{i}. {" ".If(i.ToString().Length < max.ToString().Length)}"
                              + $"({entry.state}) {" ".If(entry.state == State.Win)}"
                              + $"{" ".If(entry.score.ToString().Length < storage.ScoreEntries[min - 1].score.ToString().Length)}{entry.score} points "
                              + $"in {entry.turns} turns";

                content.Append(result + new string(' ', Math.Max(38 - result.Length, 0)) + $"- {entry.GetUsername(Context.Client)}`\n");
            }

            content.Append(max - min >= MaxDisplayedScores ? $"*Only {MaxDisplayedScores} scores may be displayed at once*" : max >= scoreAmount ? "*No more scores could be found*" : "");

            var embed = new EmbedBuilder()
            {
                Title = $"🏆 __**Global Leaderboard**__ 🏆",
                Description = content.ToString(),
                Color = new Color(241, 195, 15)
            };

            await ReplyAsync("", false, embed.Build());
        }


        [Command("score"), Alias("s", "sc"), Remarks("[user] — *See your own or another user's place on the leaderboard*")]
        [Summary("See your own highest score in the *Global Leaderboard* of all servers. You can specify a user in your guild using their name, mention or ID to see their score instead.")]
        public async Task SendPersonalBest(SocketGuildUser guildUser = null) => await SendPersonalBest((guildUser ?? Context.User).Id);

        [Command("score"), Alias("s", "sc")]
        public async Task SendPersonalBest(ulong userId)
        {
            for (int i = 0; i < storage.ScoreEntries.Count; i++)
            {
                if (storage.ScoreEntries[i].userId == userId) // The list is always kept sorted so the first match is the highest score
                {
                    var embed = new EmbedBuilder()
                    {
                        Title = $"🏆 __**Global Leaderboard**__ 🏆",
                        Description = storage.ScoreEntries[i].ToStringSimpleScoreboard(Context.Client, i + 1),
                        Color = new Color(241, 195, 15)
                    };
                    await ReplyAsync("", false, embed.Build());
                    return;
                }
            }

            await ReplyAsync("No scores registered for this user!");
        }


        [Command("custom"), Remarks("— *Learn how custom maps work*")]
        [Summary("Using this command will display detailed help about the custom maps that you can design and play yourself!")]
        [RequireBotPermission(ChannelPermission.EmbedLinks)]
        public async Task SayCustomMapHelp()
        {
            string message = storage.BotContent["customhelp"].Replace("{prefix}", storage.GetPrefixOrEmpty(Context.Guild));
            string[] links = storage.BotContent["customlinks"].Split('\n').Where(s => s.Contains("|")).ToArray();

            var embed = new EmbedBuilder() { Color = new Color(241, 195, 15) };
            for (int i = 0; i < links.Length; i++)
            {
                embed.AddField(links[i].Split('|')[0], $"[Click here]({links[i].Split('|')[1]} \"{links[i].Split('|')[1]}\")", true);
            }
            await ReplyAsync(message, false, embed.Build());
        }



        public async Task AddControls(IUserMessage message)
        {
            foreach (string input in GameInputs.Keys)
            {
                try
                {
                    await message.AddReactionAsync(input.ToEmoji());
                }
                catch (Discord.Net.RateLimitedException e)
                {
                    await logger.Log(LogSeverity.Warning, $"Ratelimit adding controls to message {message.Id} in {Context.FullChannelName()}");
                }
            }
        }
    }
}
