using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using static PacManBot.Modules.PacManModule.Game;

namespace PacManBot.Modules.PacManModule
{
    [Name("Game")]
    public class PacManModule : ModuleBase<SocketCommandContext>
    {
        [Command("start"), Summary("Start a new game")]
        [RequireBotPermission(GuildPermission.AddReactions)]
        public async Task StartGameInstance()
        {
            foreach (Game game in gameInstances)
            {
                if (Context.Channel.Id == game.channelId) //Finds a game instance corresponding to this channel
                {
                    await ReplyAsync("There is already an ongoing game on this channel!");
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
        [RequireBotPermission(GuildPermission.AddReactions)]
        public async Task RefreshGameInstance()
        {
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
        [RequireUserPermission(GuildPermission.ManageMessages)]
        public async Task EndGameInstance()
        {
            foreach (Game game in gameInstances)
            {
                if (Context.Channel.Id == game.channelId)
                {
                    gameInstances.Remove(game);
                    await ReplyAsync("Game ended.");

                    var gameMessage = await Context.Channel.GetMessageAsync(game.messageId) as IUserMessage;
                    if (gameMessage != null)
                    {
                        await gameMessage.ModifyAsync(m => m.Content = game.Display + "```diff\n-Game has been ended!```"); //Edit message
                        await gameMessage.RemoveAllReactionsAsync(); //Remove reactions
                    }
                    return;
                }
            }

            await ReplyAsync("There is no active game on this channel!");
        }


        public async Task AddControls(IUserMessage message)
        {
            await message.AddReactionAsync(new Emoji(LeftEmoji));
            await message.AddReactionAsync(new Emoji(UpEmoji));
            await message.AddReactionAsync(new Emoji(DownEmoji));
            await message.AddReactionAsync(new Emoji(RightEmoji));
            await message.AddReactionAsync(new Emoji(WaitEmoji));
            //await message.AddReactionAsync(new Emoji(RefreshEmoji));
        }
    }
}
