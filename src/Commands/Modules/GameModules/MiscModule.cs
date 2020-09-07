using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Net;
using PacManBot.Constants;
using PacManBot.Extensions;
using PacManBot.Games;
using PacManBot.Games.Concrete;

namespace PacManBot.Commands.Modules.GameModules
{
    [Name(ModuleNames.Misc), Remarks("4")]
    public class MiscModule : BaseGameModule<IChannelGame>
    {
        [Command("bump"), Alias("b", "refresh"), Priority(2)]
        [Remarks("Move any game to the bottom of the chat")]
        [Summary("Moves the current game's message in this channel to the bottom of the chat, deleting the old one." +
                 "This is useful if the game got lost in a sea of other messages, or if the game stopped responding")]
        public async Task MoveGame()
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


        [Command("cancel"), Alias("endgame"), Priority(1)]
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
                        await ReplyAsync($"Game ended.\n**Result:** {pacManGame.score} points in {pacManGame.Time} turns");
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


        [Command("dice"), Alias("die", "roll"), Parameters("[faces]")]
        [Remarks("Roll a dice")]
        [Summary("Roll a 6-sided dice, or specify a size of dice up to 100.")]
        public async Task RollDice(int faces = 6)
        {
            if (faces < 2 || faces > 100)
            {
                await ReplyAsync("The dice must have between 2 and 100 faces.");
                return;
            }
            int dice = Program.Random.Next(1, faces + 1);
            await ReplyAsync($"🎲{dice.ToString().Select(x => CustomEmoji.Number[x - '0']).JoinString()}");
        }


        [Command("coinflip"), Alias("coin", "flip", "cointoss")]
        [Remarks("Flip a coin")]
        [Summary("Flip a coin, heads or tails.")]
        public async Task CoinFlip()
        {
            await ReplyAsync(Program.Random.OneIn(2) ? "🤴 **Heads!**" : "⚖️ **Tails!**");
        }
    }
}
