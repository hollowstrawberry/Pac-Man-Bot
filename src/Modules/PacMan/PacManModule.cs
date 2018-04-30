using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using PacManBot.Services;
using PacManBot.Constants;
using PacManBot.CustomCommandAttributes;
using static PacManBot.Modules.PacMan.GameInstance;
using Discord.Net;

namespace PacManBot.Modules.PacMan
{
    [Name("🎮Game")]
    public class PacManModule : ModuleBase<SocketCommandContext>
    {
        private readonly LoggingService logger;
        private readonly StorageService storage;

        private const int MaxDisplayedScores = 20;


        public PacManModule(LoggingService logger, StorageService storage)
        {
            this.logger = logger;
            this.storage = storage;
        }



        [Command("play"), Alias("p", "game", "start"), Remarks("Start a new game on this channel"), Parameters("[mobile/m] [\\`\\`\\`custom map\\`\\`\\`]")]
        [Summary("Starts a new game, unless there is already an active game on this channel.\nAdding \"mobile\" or \"m\" after the command will begin the game in *Mobile Mode*, "
               + "which uses simple characters that will work in phones. (To change back to normal mode, use the **{prefix}refresh** command.)\nIf you add a valid customized map "
               + "between \\`\\`\\`triple backticks\\`\\`\\`, it will start a custom game using that map instead. For more information about custom games, use the **{prefix}custom** command.")]
        [RequireBotPermission(ChannelPermission.ReadMessageHistory), RequireBotPermission(ChannelPermission.UseExternalEmojis)/*, RequireBotPermission(ChannelPermission.AddReactions)*/]
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
            var gameMessage = await ReplyAsync(preMessage + newGame.GetDisplay(showHelp: false) + "```diff\n+Starting game```"); //Output the game
            newGame.messageId = gameMessage.Id;

            try
            {
                await AddControls(gameMessage);
                await gameMessage.ModifyAsync(m => m.Content = newGame.GetDisplay()); //Restore display to normal
            }
            catch (HttpException) { } // Message not found (deleted at some point)
            catch (RateLimitedException) { await logger.Log(LogSeverity.Warning, $"Ratelimit editing game message in {Context.Channel.FullName()}"); }
        }


        [Command("refresh"), Alias("ref", "r"), Remarks("Move the game to the bottom of the chat")]
        [Summary("If there is already an active game on this channel, using this command moves the game message to the bottom of the chat, and deletes the old one." +
                 "\nThis is useful if the game message has been lost in a sea of other messages or if you encounter a problem with reactions.\nAdding \"mobile\" or \"m\" " +
                 "after the command will refresh the game in *Mobile Mode*, which uses simple characters that will work in phones. Refreshing again will return it to normal.")]
        [RequireBotPermission(ChannelPermission.ReadMessageHistory), RequireBotPermission(ChannelPermission.UseExternalEmojis)/*, RequireBotPermission(ChannelPermission.AddReactions)*/]
        public async Task RefreshGameInstance([Name("mobile/m")] string arg = "")
        {
            foreach (GameInstance game in storage.GameInstances)
            {
                if (Context.Channel.Id == game.channelId) //Finds a game instance corresponding to this channel
                {
                    var oldMsg = await Context.Channel.GetMessageAsync(game.messageId);
                    if (oldMsg != null) await oldMsg.DeleteAsync(); //Delete old message

                    game.mobileDisplay = arg.StartsWith("m");
                    var newMsg = await ReplyAsync(game.GetDisplay(showHelp: false) + "```diff\n+Refreshing game```"); //Send new message
                    game.messageId = newMsg.Id; //Change focus message for this channel

                    try
                    {
                        await AddControls(newMsg);
                        await newMsg.ModifyAsync(m => m.Content = game.GetDisplay()); //Restore display to normal
                    }
                    catch (HttpException) { } // Message not found (deleted at some point)
                    catch (RateLimitedException) { await logger.Log(LogSeverity.Warning, $"Ratelimit editing game message in {Context.Channel.FullName()}"); }

                    return;
                }
            }

            await ReplyAsync("There is no active game on this channel!");
        }


        [Command("end"), Alias("stop"), Remarks("End a game you started. Always usable by moderators")]
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


        [Command("leaderboard"), Alias("lb", "l"), Parameters("[all/month/week/day] [start] [end]")]
        [Remarks("Global Leaderboard scores"), ExampleUsage("leaderboard 5\nlb month 11 30")]
        [Summary("By default, displays the top 10 scores of all time from the Global Leaderboard of all servers.\n"
               + "You can specify a time period to display scores from: all/month/week/day (a/m/w/d are also valid). "
               + "You can also specify a range of scores from [start] to [end], where those are two positive numbers.\n"
               + "Only 20 scores may be displayed at once. If given just one number, it will be taken as the start if it's above 20, or as the end otherwise.")]
        public async Task SendTopScores(int min = 10, int? max = null) => await SendTopScores(Utils.TimePeriod.all, min, max);

        [Command("leaderboard"), Alias("lb", "l")]
        public async Task SendTopScores(Utils.TimePeriod time, int min = 10, int? max = null)
        {
            if (min < 1 || max < 1 || max < min)
            {
                await ReplyAsync($"Invalid range of scores. Try **{storage.GetPrefixOrEmpty(Context.Guild)}help lb** for more info.");
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
                await ReplyAsync("There are no registered scores within this period!");
                return;
            }

            if (min > scores.Count)
            {
                await ReplyAsync("No scores found within the specified range.");
                return;
            }

            var content = new StringBuilder();
            content.Append($"Displaying best scores {Utils.ScorePeriodString(time)}\nᅠ\n");

            for (int i = min; i < scores.Count && i <= max && i < min + MaxDisplayedScores; i++)
            {
                ScoreEntry entry = scores[i - 1]; // The list is always kept sorted so we just go by index

                //Aligns elements
                string result = $"`{i}. {" ".If(i.ToString().Length < max.ToString().Length)}"
                              + $"({entry.state}) {" ".If(entry.state == State.Win)}"
                              + $"{" ".If(entry.score.ToString().Length < storage.ScoreEntries[min - 1].score.ToString().Length)}{entry.score} points "
                              + $"in {entry.turns} turns";
                //Aligns names
                content.Append(result + new string(' ', Math.Max(38 - result.Length, 0)) + $"- {entry.GetUsername(Context.Client)}`\n");
            }

            content.Append(max - min >= MaxDisplayedScores && max < scores.Count ? $"*Only {MaxDisplayedScores} scores may be displayed at once*"
                           : max >= scores.Count ? "*No more scores could be found*" : "");


            var embed = new EmbedBuilder()
            {
                Title = $"🏆 __**Global Leaderboard**__ 🏆",
                Description = content.ToString(),
                Color = new Color(241, 195, 15)
            };

            await ReplyAsync("", false, embed.Build());
        }


        [Command("score"), Alias("sc", "s"), Parameters("[all/month/week/day] [user]")]
        [ExampleUsage("score d\nsc week @Samrux#3980"), Remarks("See your or a person's top score")]
        [Summary("See your own highest score of all time in the *Global Leaderboard* of all servers. You can specify a user in your guild using their name, mention or ID, to see their score instead."
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
                        Title = $"🏆 __**Global Leaderboard**__ 🏆",
                        Description = $"Highest score {Utils.ScorePeriodString(time)}:\n{scores[i].ToStringSimpleScoreboard(Context.Client, i + 1)}",
                        Color = new Color(241, 195, 15)
                    };
                    await ReplyAsync("", false, embed.Build());
                    return;
                }
            }

            await ReplyAsync(time == Utils.TimePeriod.all ? "No scores registered for this user!" : "No scores registered during that time!");
        }


        [Command("custom"), Remarks("Learn how custom maps work")]
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
            foreach (IEmote input in GameInputs.Keys)
            {
                try
                {
                    await message.AddReactionAsync(input);
                }
                catch (RateLimitedException)
                {
                    await logger.Log(LogSeverity.Warning, $"Ratelimit adding controls to game message in {Context.Channel.FullName()}");
                }
            }
        }
    }
}
