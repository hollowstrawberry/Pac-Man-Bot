using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Net;
using PacManBot.Extensions;
using PacManBot.Games;
using PacManBot.Games.Concrete;

namespace PacManBot.Commands.Modules.GameModules
{
    [Name(ModuleNames.Games), Remarks("3")]
    public class CommonGameModule : BaseGameModule<IChannelGame>
    {
        [Command("bump"), Alias("b", "refresh"), Priority(-4)]
        [Remarks("Move any game to the bottom of the chat")]
        [Summary("Moves the current game's message in this channel to the bottom of the chat, deleting the old one." +
                 "This is useful if the game got lost in a sea of other messages, or if the game stopped responding")]
        private async Task MoveGame()
        {
            if (Game == null)
            {
                await ReplyAsync("There is no active game in this channel!");
                return;
            }

            await DeleteGameMessageAsync();
            var msg = await ReplyGameAsync();

            if (Game is PacManGame pacmanGame) await PacManModule.AddControls(pacmanGame, msg);
        }


        [Command("cancel"), Alias("endgame"), Priority(-5)]
        [Remarks("Cancel any game you're playing. Always usable by moderators")]
        [Summary("Cancels the current game in this channel, but only if you started or if nobody has played in over a minute. " +
                 "Always usable by users with the Manage Messages permission.")]
        public async Task CancelGame()
        {
            if (Game == null)
            {
                await ReplyAsync("There is no active game in this channel!");
                return;
            }

            if (Game.UserId.Contains(Context.User.Id) || Context.UserCan(ChannelPermission.ManageMessages)
                || DateTime.Now - Game.LastPlayed > TimeSpan.FromSeconds(60) || Game is MultiplayerGame mpGame && mpGame.AllBots)
            {
                EndGame();
                var msg = await UpdateGameMessageAsync();

                if (Game is PacManGame pacManGame)
                {
                    if (Context.Guild != null)
                    {
                        await ReplyAsync($"Game ended. Score won't be registered.\n**Result:** {pacManGame.score} points in {pacManGame.Time} turns");
                    }
                    if (msg != null && Context.BotCan(ChannelPermission.ManageMessages))
                    {
                        try { await msg.RemoveAllReactionsAsync(DefaultOptions); }
                        catch (HttpException) { }
                    }
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
