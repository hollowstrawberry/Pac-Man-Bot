using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using PacManBot.Constants;
using PacManBot.Extensions;
using PacManBot.Games;
using PacManBot.Games.Concrete;

namespace PacManBot.Commands.Modules
{
    [Category(Categories.Games)]
    [Group("uno")]
    [Description("Play Uno with your friends or bots!\n" +
    "__Tip__: Switching back and forth with DMs to see your cards can be tiresome, " +
    "so try having your cards open in your phone while you're playing in a computer.")]
    [RequireBotPermissions(BaseBotPermissions)]
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Command reflection")]
    public class UnoModule : BaseMultiplayerModule<UnoGame>
    {
        [GroupCommand, Priority(3)]
        public async Task StartUno(CommandContext ctx, params DiscordMember[] startingPlayers)
        {
            if (ctx.Guild is null)
            {
                await StartNewMPGameAsync(ctx, ctx.User, ctx.Client.CurrentUser);
            }
            else
            {
                await StartNewMPGameAsync(ctx, new[] { ctx.Member }.Concatenate(startingPlayers));
            }
        }

        [Command("rules"), Aliases("manual", "help")]
        [Description("Show the game's rules")]
        public async Task UnoRules(CommandContext ctx)
        {
            await ctx.RespondAsync($"\n\n__**{CustomEmoji.UnoWild} Uno Rules:**__\n" +
                "\n • Each player is given 7 cards." +
                "\n • The current turn's player must choose to discard a card that matches either the color, number or type of the last card." +
                "\n • If the player doesn't have any matching card, or they don't want to discard any of their cards, " +
                "they can say \"**draw**\" to draw a card. That card will be discarded immediately if possible." +
                "\n • When you only have one card left, you must say \"**uno**\" (You can add it to your card like \"red4 uno\", " +
                "or you can say it directly after if you forget). If you forget, someone else can say \"uno\" to call you out before the " +
                "next player plays, and you will draw 2 cards." +
                "\n • The first player to lose all of their cards wins the game." +
                "\n • **Special cards:** *Skip* cards make the next player skip a turn. *Reverse* cards change the turn direction, " +
                "or act like Skip cards with only two players." +
                " *Draw* cards force the next player to draw cards and skip a turn. *Wild* cards let you choose the color, " +
                "and will match with any card.");
        }


        [Command("join"), Aliases("add", "invite"), Priority(-1)]
        [Description("Joins an ongoing Uno game in this channel. Fails if the game is full or if there aren't enough cards to draw for you.\n" +
                 "You can also invite a bot or another user to play.")]
        [RequireGuild]
        public async Task JoinUno(CommandContext ctx, DiscordMember member = null)
        {
            bool self = false;
            if (member is null)
            {
                self = true;
                member = ctx.Member;
            }

            if (Game(ctx) is null)
            {
                await ctx.RespondAsync($"There's no Uno game in this channel! Use `{ctx.Prefix}uno` to start.");
                return;
            }
            if (Game(ctx).UserId.Contains(member.Id))
            {
                await ctx.RespondAsync($"{(self ? "You're" : "They're")} already playing!");
                return;
            }
            if (!self && !member.IsBot)
            {
                await ctx.RespondAsync($"{member.Mention} You're being invited to play {Game(ctx).GameName}.\nDo `{Storage.GetPrefix(ctx)}uno join` to join.");
                return;
            }

            string failReason = await Game(ctx).TryAddPlayerAsync(member);

            if (failReason is null)
            {
                await DeleteGameMessageAsync(ctx);
                await RespondGameAsync(ctx);
            }
            else
            {
                await ctx.RespondAsync($"{member.Mention} {"You ".If(self)}can't join this game: {failReason}");
            }

            if (ctx.BotCan(Permissions.ManageMessages)) await ctx.Message.DeleteAsync();
            else await ctx.AutoReactAsync(failReason is null);
        }


        [Command("leave"), Priority(-1)]
        [Description("Lets you leave the Uno game in this channel.")]
        [RequireGuild]
        public async Task LeaveUno(CommandContext ctx)
        {
            var game = Game(ctx);
            if (game is null)
            {
                await ctx.RespondAsync($"There's no Uno game in this channel! Use `uno` to start.");
                return;
            }
            if (!game.UserId.Contains(ctx.User.Id))
            {
                await ctx.RespondAsync($"You're not in the game.");
                return;
            }

            await game.RemovePlayerAsync(ctx.User);

            if (game.State == GameState.Cancelled)
            {
                await UpdateGameMessageAsync(ctx);
                EndGame(ctx);
            }
            else
            {
                await DeleteGameMessageAsync(ctx);
                await RespondGameAsync(ctx);
            }

            await ctx.AutoReactAsync();
        }


        [Command("kick"), Aliases("remove"), Priority(-1)]
        [Description("Lets you kick another player or bot in the current Uno game.")]
        [RequireGuild]
        public async Task KickUno(CommandContext ctx, DiscordMember member = null)
        {
            var game = Game(ctx);
            if (game is null)
            {
                await ctx.RespondAsync($"There's no Uno game in this channel! Use `{ctx.Prefix}uno` to start.");
                return;
            }
            if (member is null)
            {
                await ctx.RespondAsync($"You must specify a user to kick from the game.");
                return;
            }
            if (!game.UserId.Contains(member.Id))
            {
                await ctx.RespondAsync($"That user is not in the game.");
                return;
            }
            if (!member.IsBot && (game.UserId[Game(ctx).Turn] != member.Id || (DateTime.Now - game.LastPlayed) < TimeSpan.FromMinutes(1)))
            {
                await ctx.RespondAsync("To remove another user they must be inactive for at least 1 minute during their turn.");
            }

            await game.RemovePlayerAsync(member);

            if (game.State == GameState.Cancelled)
            {
                await UpdateGameMessageAsync(ctx);
                EndGame(ctx);
            }
            else
            {
                await DeleteGameMessageAsync(ctx);
                await RespondGameAsync(ctx);
            }

            await ctx.AutoReactAsync();
        }
    }
}
