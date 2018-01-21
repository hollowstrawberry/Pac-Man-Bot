using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using static PacManBot.Modules.PacManModule.Game;

namespace PacManBot.Modules.PacManModule
{
    [Name("Game")]
    public class GameModule : ModuleBase<SocketCommandContext>
    {
        [Command("play"), Alias("p", "start"), Summary("Start a new game on this channel")]
        public async Task StartGameInstance()
        {
            if (Context.Guild != null && !Context.Guild.CurrentUser.GuildPermissions.AddReactions)
            {
                await ReplyAsync("This bot requires the permission to add reactions!");
                return;
            }

            foreach (Game game in gameInstances)
            {
                if (Context.Channel.Id == game.channelId) //Finds a game instance corresponding to this channel
                {
                    await ReplyAsync("There is already an ongoing game on this channel!\nYou could use the **refresh** command to bring it to the bottom of the chat.");
                    return;
                }
            }

            Game newGame = new Game(Context.Channel.Id); //Create a game instance
            gameInstances.Add(newGame);

            var gameMessage = await ReplyAsync(newGame.Display + "```diff\n+Starting game```"); //Output the game
            newGame.messageId = gameMessage.Id;
            await AddControls(gameMessage); //Controls for easy access
            await gameMessage.ModifyAsync(m => m.Content = newGame.Display); //Edit message
        }

        [Command("refresh"), Alias("r"), Summary("Move the game to the bottom of the chat")]
        public async Task RefreshGameInstance()
        {
            if (Context.Guild != null && !Context.Guild.CurrentUser.GuildPermissions.AddReactions)
            {
                await ReplyAsync("This bot requires the permission to add reactions!");
                return;
            }

            foreach (Game game in gameInstances)
            {
                if (Context.Channel.Id == game.channelId) //Finds a game instance corresponding to this channel
                {
                    var oldMsg = await Context.Channel.GetMessageAsync(game.messageId);
                    if (oldMsg != null) await oldMsg.DeleteAsync(); //Delete old message
                    var newMsg = await ReplyAsync(game.Display + "```diff\n+Refreshing game```"); //Send new message
                    game.messageId = newMsg.Id; //Change focus message for this channel
                    await AddControls(newMsg);
                    await newMsg.ModifyAsync(m => m.Content = game.Display); //Edit message
                    return;
                }
            }

            await ReplyAsync("There is no active game on this channel!");
        }

        [Command("end"), Alias("stop"), Summary("End the current game (Moderator)")]
        public async Task EndGameInstance()
        {
            if (Context.Guild != null && !Context.Guild.CurrentUser.GuildPermissions.ManageMessages)
            {
                await ReplyAsync("You must be a Moderator to use this command!");
                return;
            }

            foreach (Game game in gameInstances)
            {
                if (Context.Channel.Id == game.channelId)
                {
                    gameInstances.Remove(game);
                    await ReplyAsync("Game ended.");

                    if (await Context.Channel.GetMessageAsync(game.messageId) is IUserMessage gameMessage)
                    {
                        await gameMessage.ModifyAsync(m => m.Content = game.Display + "```diff\n-Game has been ended!```"); //Edit message
                        await gameMessage.RemoveAllReactionsAsync(); //Remove reactions
                    }
                    return;
                }
            }

            await ReplyAsync("There is no active game on this channel!");
        }

        [Command("leaderboard"), Alias("l"), Summary("Global list of top scores. You can specify amount or a start and end")]
        public async Task SendTopScores(int amount = 10) => await SendTopScores(1, amount);

        [Command("leaderboard"), Alias("l")]
        public async Task SendTopScores(int min, int max)
        {
            if (min <= 1) min = 1;
            if (max < min) max = min + 9;

            string[] scoreLine = File.ReadAllLines(Program.File_Scoreboard).Skip(1).ToArray(); //Skips the first line
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

            if (max - min > 19) message += "\n*Only 20 scores may be displayed at once*";

            if (message.Length > 2000) message = message.Substring(0, 1999);

            await ReplyAsync(message);
        }

        [Command("score"), Alias("s"), Summary("See your own or another person's place on the leaderboard")]
        public async Task SendPersonalBest(SocketGuildUser guildUser = null)
        {
            SocketUser user;
            if (guildUser == null) user = Context.User;
            else user = guildUser;

            string[] scoreLine = File.ReadAllLines(Program.File_Scoreboard).Skip(1).ToArray(); //Skips the first line
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
                if (scoreLine[i].Split(' ')[3] == user.Id.ToString() && score[i] > topScore)
                {
                    topScore = score[i];
                    topScoreIndex = i;
                }
            }

            string[] splitLine = scoreLine[topScoreIndex].Split(' ');
            await ReplyAsync(topScore == 0 ? ((guildUser == null ? "You don't have" : "The user doesn't have") + " any scores registered!") : $"🏆 __**Global Leaderboard**__\n{topScoreIndex + 1}. ({splitLine[0]}) **{splitLine[1]}** in {splitLine[2]} turns by user " + (user == null ? "Unknown" : $"{user.Username}#{user.Discriminator}"));
        }

        [Command("tips"), Summary("Learn some secrets that will help you")]
        public async Task SayTips() => await ReplyAsync(File.ReadAllText(Program.FileTips));


        public async Task AddControls(IUserMessage message)
        {
            await message.AddReactionAsync(new Emoji(LeftEmoji));
            await message.AddReactionAsync(new Emoji(UpEmoji));
            await message.AddReactionAsync(new Emoji(DownEmoji));
            await message.AddReactionAsync(new Emoji(RightEmoji));
            await message.AddReactionAsync(new Emoji(WaitEmoji));
        }
    }
}