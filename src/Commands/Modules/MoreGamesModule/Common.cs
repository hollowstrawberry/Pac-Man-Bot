using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Net;
using PacManBot.Extensions;
using PacManBot.Games;
using PacManBot.Games.Concrete;
using PacManBot.Services;

namespace PacManBot.Commands.Modules
{
    [Name("👾More Games"), Remarks("3")]
    [PmRequireBotPermission(ChannelPermission.ReadMessageHistory | ChannelPermission.EmbedLinks |
                                ChannelPermission.UseExternalEmojis | ChannelPermission.AddReactions)]
    public partial class MoreGamesModule : PmBaseModule
    {
        public IServiceProvider Services { get; set; }
        public PmCommandService Commands { get; set; }


        [Command("bump"), Alias("b", "refresh", "r", "move"), Priority(-4)]
        [Remarks("Move any game to the bottom of the chat")]
        [Summary("Moves the current game's message in this channel to the bottom of the chat, deleting the old one." +
                 "This is useful if the game got lost in a sea of other messages, or if the game stopped responding")]
        private async Task MoveGame()
        {
            var game = Games.GetForChannel(Context.Channel.Id);
            if (game == null)
            {
                await ReplyAsync("There is no active game in this channel!");
                return;
            }

            try
            {
                var gameMessage = await game.GetMessage();
                if (gameMessage != null) await gameMessage.DeleteAsync(DefaultOptions); // Old message
            }
            catch (HttpException) { } // Something happened to the message, can ignore it

            var message = await ReplyAsync(game.GetContent(), game.GetEmbed());
            game.MessageId = message.Id;

            if (game is PacManGame pacmanGame) await PacManModule.AddControls(pacmanGame, message);
        }


        [Command("cancel"), Alias("end"), Priority(-5)]
        [Remarks("Cancel any game you're playing. Always usable by moderators")]
        [Summary("Cancels the current game in this channel, but only if you started or if nobody has played in over a minute. " +
                 "Always usable by users with the Manage Messages permission.")]
        public async Task CancelGame()
        {
            var game = Games.GetForChannel(Context.Channel.Id);
            if (game == null)
            {
                await ReplyAsync("There is no active game in this channel!");
                return;
            }

            if (game.UserId.Contains(Context.User.Id) || Context.UserCan(ChannelPermission.ManageMessages)
                || DateTime.Now - game.LastPlayed > TimeSpan.FromSeconds(60) || game is MultiplayerGame mpGame && mpGame.AllBots)
            {
                game.State = State.Cancelled;
                Games.Remove(game);

                try
                {
                    var gameMessage = await game.GetMessage();
                    if (gameMessage != null)
                    {
                        await gameMessage.ModifyAsync(game.GetMessageUpdate(), DefaultOptions);

                        if (game is PacManGame && Context.BotCan(ChannelPermission.ManageMessages))
                        {
                            await gameMessage.RemoveAllReactionsAsync(DefaultOptions);
                        }
                    }
                }
                catch (HttpException) { } // Something happened to the message, we can ignore it

                if (game is PacManGame pacManGame && Context.Guild != null)
                {
                    await ReplyAsync($"Game ended. Score won't be registered.\n**Result:** {pacManGame.score} points in {pacManGame.Time} turns");
                }
                else
                {
                    await AutoReactAsync();
                }
            }
            else
            {
                await ReplyAsync("You can't cancel this game because someone else is still playing! Try again in a minute.");
            }
        }


        [Command("play"), Parameters(""), HideHelp]
        [Summary("Do **{prefix}help** for games and commands.")]
        public async Task PlayMessage([Remainder]string args = "")
        {
            await ReplyAsync($"To see a list of commands you can use and games you can play, do **{Context.Prefix}help**");
        }
    }
}
