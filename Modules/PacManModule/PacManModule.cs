using Discord;
using Discord.Commands;
using System.Collections.Generic;
using System.Threading.Tasks;
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
            foreach (Game game in Game.gameInstances)
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
                    var oldMsg = await Context.Channel.GetMessageAsync(game.messageId) as IUserMessage;
                    await oldMsg.DeleteAsync(); //Delete old message
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
                    var gameMessage = await Context.Channel.GetMessageAsync(game.messageId) as IUserMessage;
                    await gameMessage.ModifyAsync(m => m.Content = game.Display + "```diff\n-Game has been ended!```"); //Edit message
                    gameInstances.Remove(game);
                    await gameMessage.RemoveAllReactionsAsync(); //Remove reactions
                    await ReplyAsync("Game ended.");
                    return;
                }
            }

            await ReplyAsync("There is no active game on this channel!");
        }

        [Command("clear"), Alias("c"), Summary("Clear all messages from this bot (Moderator)")]
        [RequireUserPermission(ChannelPermission.ManageMessages)]
        public async Task ClearGameMessages(int amount = 10)
        {
            var messages = await Context.Channel.GetMessagesAsync(amount).Flatten();
            foreach (IMessage message in messages)
            {
                if (message.Author.Id == Context.Client.CurrentUser.Id) await message.DeleteAsync(); //Remove all messages from this bot
            }
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
