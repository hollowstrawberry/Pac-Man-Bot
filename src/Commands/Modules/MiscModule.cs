using System;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.Exceptions;
using PacManBot.Constants;
using PacManBot.Extensions;
using PacManBot.Games;
using PacManBot.Games.Concrete;

namespace PacManBot.Commands.Modules
{
    [Category(Categories.Misc)]
    [RequireBotPermissions(BaseBotPermissions)]
    public class MiscModule : BaseGameModule<ChannelGame>
    {
        [Command("bump"), Aliases("b", "refresh"), Priority(2)]
        [Description(
            "Moves the current game's message in this channel to the bottom of the chat, deleting the old one." +
            "This is useful if the game got lost in a sea of other messages, or if the game stopped responding")]
        public async Task MoveGame(CommandContext ctx)
        {
            var game = Game(ctx);
            if (game == null)
            {
                await ctx.RespondAsync("There is no active game in this channel!");
                return;
            }

            if (DateTime.Now - game.LastBumped > TimeSpan.FromSeconds(2))
            {
                game.LastBumped = DateTime.Now;
                await DeleteGameMessageAsync(ctx);
                game.LastBumped = DateTime.Now;
                var msg = await RespondGameAsync(ctx);
                game.LastBumped = DateTime.Now;

                if (game is PacManGame pacmanGame) await PacManModule.AddControls(pacmanGame, msg);
            }
        }


        [Command("cancel"), Aliases("endgame"), Priority(1)]
        [Description("Cancels the current game in this channel, but only if you started or if nobody has played in over a minute. " +
                     "Always usable by users with the Manage Messages permission.")]
        public async Task CancelGame(CommandContext ctx)
        {
            var game = Game(ctx);
            if (game == null)
            {
                await ctx.RespondAsync("There is no active game in this channel!");
                return;
            }

            if (game.UserId.Contains(ctx.User.Id) || ctx.UserCan(Permissions.ManageMessages)
                || DateTime.Now - game.LastPlayed > TimeSpan.FromSeconds(60))
            {
                var msg = await game.GetMessageAsync();
                EndGame(ctx);
                if (msg != null) await game.UpdateMessageAsync();

                if (game is PacManGame pacManGame)
                {
                    if (ctx.Guild != null)
                    {
                        await ctx.RespondAsync($"Game ended.\n**Result:** {pacManGame.score} points in {pacManGame.Time} turns");
                    }
                    if (msg != null && ctx.BotCan(Permissions.ManageMessages))
                    {
                        try { await msg.DeleteAllReactionsAsync(); }
                        catch (NotFoundException) { }
                    }
                }
                else
                {
                    await ctx.AutoReactAsync();
                }
            }
            else
            {
                await ctx.RespondAsync("You can't cancel this game because someone else is still playing! Try again in a minute.");
            }
        }


        [Command("dice"), Aliases("die", "roll")]
        [Description("Roll a 6-sided dice, or specify a size of dice up to 100.")]
        public async Task RollDice(CommandContext ctx, int faces = 6)
        {
            if (faces < 2 || faces > 100)
            {
                await ctx.RespondAsync("The dice must have between 2 and 100 faces.");
                return;
            }
            int dice = Program.Random.Next(1, faces + 1);
            await ctx.RespondAsync($"🎲{dice.ToString().Select(x => CustomEmoji.Number[x - '0']).JoinString()}");
        }


        [Command("coinflip"), Aliases("coin", "flip", "cointoss")]
        [Description("Flip a coin, heads or tails.")]
        public async Task CoinFlip(CommandContext ctx)
        {
            await ctx.RespondAsync(Program.Random.OneIn(2) ? "🤴 **Heads!**" : "⚖️ **Tails!**");
        }


        [Command("party"), Aliases("blob", "dance"), Hidden]
        [Description("Takes a number which can be either an amount of emotes to send or a message ID to react to. " +
                 "Reacts to the command by default.")]
        public async Task BlobDance(CommandContext ctx, ulong number = 0)
        {
            if (number < 1) await ctx.Message.CreateReactionAsync(CustomEmoji.EBlobDance);
            else if (number <= 5) await ctx.RespondAsync(CustomEmoji.BlobDance.Repeat((int)number));
            else if (number <= 1000000) await ctx.RespondAsync("That's too many.");
            else if (ctx.UserCan(Permissions.ManageMessages)) // Message ID
            {
                if (await ctx.Channel.GetMessageAsync(number) is DiscordMessage message)
                {
                    await message.CreateReactionAsync(CustomEmoji.EBlobDance);
                }
                else await ctx.AutoReactAsync(false);
            }
        }


        [Command("neat"), Description("Neat"), Hidden]
        public async Task Neat(CommandContext ctx, [RemainingText] string neat = "")
        {
            await ctx.RespondAsync("neat");
        }


        [Command("nice"), Description("Nice"), Hidden]
        public async Task Nice(CommandContext ctx, [RemainingText] string nice = "")
        {
            await ctx.RespondAsync("nice");
        }
    }
}
