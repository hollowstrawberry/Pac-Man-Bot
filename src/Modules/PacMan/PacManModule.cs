using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using PacManBot.Services;
using PacManBot.Constants;
using static PacManBot.Modules.PacMan.PacManGame;

namespace PacManBot.Modules.PacMan
{
    [Name("🎮Game")]
    public class PacManModule : ModuleBase<SocketCommandContext>
    {
        private readonly LoggingService logger;
        private readonly StorageService storage;

        public PacManModule(LoggingService logger, StorageService storage)
        {
            this.logger = logger;
            this.storage = storage;
        }


        private string ManualModeMessage => "__Manual mode:__ Both adding and removing reactions count as input. Do one action at a time to prevent buggy behavior." + "\nGive this bot the permission to Manage Messages to remove reactions automatically.".If(Context.Guild != null);


        [Command("play"), Alias("p"), Remarks("[mobile/m] [\\`\\`\\`custom map\\`\\`\\`] — *Start a new game on this channel*")]
        [Summary("Starts a new game, unless there is already an active game on this channel.\nAdding \"mobile\" or \"m\" after the command will begin the game in *Mobile Mode*, which uses simple characters that will work in phones. (To change back to normal mode, use the **{prefix}refresh** command.)\nIf you add a valid customized map between \\`\\`\\`triple backticks\\`\\`\\`, it will start a custom game using that map instead. For more information about custom games, use the **{prefix}custom** command.")]
        [RequireBotPermission(ChannelPermission.ReadMessageHistory | ChannelPermission.AddReactions)]
        public async Task StartGameInstance([Remainder]string args = "")
        {
            if (Context.Guild != null && !Context.BotHas(ChannelPermission.SendMessages)) return;

            string prefix = storage.GetPrefixOrEmpty(Context.Guild);

            foreach (PacManGame game in storage.gameInstances)
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

            PacManGame newGame;
            try
            {
                newGame = new PacManGame(Context.Channel.Id, Context.User.Id, customMap, Context.Client, storage, logger);
            }
            catch (InvalidMapException e)
            {
                await logger.Log(LogSeverity.Debug, "Game", $"{e}");
                await ReplyAsync($"The provided map is invalid: {e.Message}.\n{"Use the **{prefix}custom** command for more info.".If(customMap != null)}");
                return;
            }
            catch (Exception e)
            {
                await logger.Log(LogSeverity.Error, "Game", $"{e}");
                await ReplyAsync($"There was an error starting the game. Please try again or contact the author of the bot using **{prefix}feedback**");
                return;
            }

            storage.gameInstances.Add(newGame);
 
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
            foreach (PacManGame game in storage.gameInstances)
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
            foreach (PacManGame game in storage.gameInstances)
            {
                if (Context.Channel.Id == game.channelId)
                {
                    if (game.ownerId == Context.User.Id || Context.Guild != null && Context.UserHas(ChannelPermission.ManageMessages))
                    {
                        if (File.Exists(game.GameFile)) File.Delete(game.GameFile);
                        storage.gameInstances.Remove(game);
                        await ReplyAsync("Game ended.");

                        if (await Context.Channel.GetMessageAsync(game.messageId) is IUserMessage gameMessage)
                        {
                            if (Context.Guild != null) await gameMessage.DeleteAsync(); //So as to not leave spam in guild channels
                            else await gameMessage.ModifyAsync(m => m.Content = game.GetDisplay() + "```diff\n-Game has been ended!```"); //Edit message
                        }
                    }
                    else await ReplyAsync("You can't end this game because you didn't start it!");

                    return;
                }
            }

            await ReplyAsync("There is no active game on this channel!");
        }


        [Command("leaderboard"), Alias("l"), Remarks("[start] [end] — *Global list of top scores. You can enter a range*")]
        [Summary("This command will display a list of scores in the *Global Leaderboard* of all servers.\nIt goes from 1 to 10 by default, but you can specify an end and start point for any range of scores.")]
        public async Task SendTopScores(string amount = "10") => await SendTopScores("1", amount);

        [Command("leaderboard"), Alias("l")]
        public async Task SendTopScores(string smin, string smax)
        {
            if (!int.TryParse(smin, out int min) | !int.TryParse(smax, out int max))
            {
                // So like people weren't understanding how to use this command so I had to handle it myself to tell them
                await ReplyAsync("You must enter one or two whole numbers!");
                return;
            }

            if (min <= 1) min = 1;
            if (max < min) max = min + 9;

            string[] scoreLine = File.ReadAllLines(BotFile.Scoreboard).Skip(1).Where(s => !string.IsNullOrWhiteSpace(s)).ToArray(); //Skips the first line and empty lines
            int scoresAmount = scoreLine.Length;
            string[] scoreText = new string[scoresAmount];
            int[] score = new int[scoresAmount];


            if (scoreLine.Length < 1)
            {
                await ReplyAsync("There are no registered scores! Go make one");
                return;
            }

            if (min > scoresAmount)
            {
                await ReplyAsync("No scores found within the specified range.");
                return;
            }

            for (int i = 0; i < scoresAmount; i++)
            {
                string[] splitLine = scoreLine[i].Split(' '); //Divide into sections
                for (int j = 0; j < splitLine.Length; j++) splitLine[j].Trim(); //Trim the ends

                var user = Context.Client.GetUser(ulong.Parse(splitLine[3])); //Third section is the user id
                scoreText[i] = $"({splitLine[0]}) **{splitLine[1]}** in {splitLine[2]} turns by user " + (user == null ? "Unknown" : $"{user.Username}#{user.Discriminator}");
                score[i] = Int32.Parse(splitLine[1]);
            }

            Array.Sort(score, scoreText);
            Array.Reverse(scoreText);

            string message = $"🏆 __**Global Leaderboard**__";
            for (int i = min; i < scoresAmount && i <= max && i < min + 20; i++) //Caps at 20
            {
                message += $"\n{i}. {scoreText[i - 1]}";
            }

            if (max >= scoresAmount) message += "\n*No more scores could be found*";
            else if (max - min > 19) message += "\n*Only 20 scores may be displayed at once*";

            if (message.Length > 2000) message = message.Substring(0, 1999);

            await ReplyAsync(message);
        }


        [Command("score"), Alias("s"), Remarks("[user] — *See your own or another user's place on the leaderboard*")]
        [Summary("See your own highest score in the *Global Leaderboard* of all servers. You can specify a user in your guild using their name, mention or ID to see their score instead.")]
        public async Task SendPersonalBest(SocketGuildUser guildUser = null) => await SendPersonalBest((guildUser ?? Context.User).Id);

        [Command("score"), Alias("s")]
        public async Task SendPersonalBest(ulong userId)
        {
            string[] scoreLine = File.ReadAllLines(BotFile.Scoreboard).Skip(1).ToArray(); //Skips the first line
            int scoresAmount = scoreLine.Length;
            int[] score = new int[scoresAmount];

            for (int i = 0; i < scoresAmount; i++)
            {
                score[i] = Int32.Parse(scoreLine[i].Split(' ')[1].Trim());
            }

            Array.Sort(score, scoreLine);
            Array.Reverse(scoreLine);
            Array.Reverse(score);

            int topScore = 0;
            int topScoreIndex = 0;
            for (int i = 0; i < scoresAmount; i++)
            {
                if (scoreLine[i].Split(' ')[3] == userId.ToString() && score[i] > topScore)
                {
                    topScore = score[i];
                    topScoreIndex = i;
                }
            }

            string[] splitLine = scoreLine[topScoreIndex].Split(' ');
            var user = Context.Client.GetUser(userId);
            await ReplyAsync(topScore == 0 ? "No scores registered!" : $"🏆 __**Global Leaderboard**__\n{topScoreIndex + 1}. ({splitLine[0]}) **{splitLine[1]}** in {splitLine[2]} turns by user " + (user == null ? "Unknown" : user.FullName()));
        }


        [Command("custom"), Remarks("— *Learn how custom maps work*")]
        [Summary("Using this command will display detailed help about the custom maps that you can design and play yourself!")]
        [RequireBotPermission(ChannelPermission.EmbedLinks)]
        public async Task SayCustomMapHelp()
        {
            string fileText = File.ReadAllText(BotFile.Contents);
            string message = fileText.FindValue("customhelp").Replace("{prefix}", storage.GetPrefixOrEmpty(Context.Guild));
            string[] links = fileText.FindValue("customlinks").Split('\n').Where(s => s.Contains("|")).ToArray();

            var embed = new EmbedBuilder() { Color = new Color(241, 195, 15) };
            for (int i = 0; i < links.Length; i++)
            {
                embed.AddField(links[i].Split('|')[0], $"[Click here]({links[i].Split('|')[1]} \"{links[i].Split('|')[1]}\")", true);
            }
            await ReplyAsync(message, false, embed.Build());
        }



        public async Task AddControls(IUserMessage message)
        {
            foreach (string input in gameInput.Keys)
            {
                await message.AddReactionAsync(input.ToEmoji());
            }
        }
    }
}
