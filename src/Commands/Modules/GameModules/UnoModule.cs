using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using PacManBot.Extensions;
using PacManBot.Games.Concrete;

namespace PacManBot.Commands.Modules
{
    [Group(ModuleNames.Games), Description("3")]
    public class UnoModule : MultiplayerGameModule<UnoGame>
    {
        [Command("uno"), Priority(3)]
        [Description(
            "__Tip__: Switching back and forth with DMs to see your cards can be tiresome, " +
            "so try having your cards open in your phone while you're playing in a computer." +
            "\n\n__**Commands:**__\n" +
            "\n • **{prefix}uno** - Starts a new Uno game for up to 10 players. You can specify players and bots as players. " +
            "Players can join or leave at any time." +
            "\n • **{prefix}uno bots** - Starts a bot-only game with the specified bots." +
            "\n • **{prefix}uno join** - Join a game or invite a user or bot." +
            "\n • **{prefix}uno leave/kick** - Leave the game or kick a bot or inactive user." +
            "\n • **{prefix}bump** - Move the game to the bottom of the chat." +
            "\n • **{prefix}cancel** - End the game in the current channel." +
            "\n\n\n__**Rules:**__\n" +
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
            "and will match with any card.")]
        public async Task StartUno(CommandContext ctx, params DiscordMember[] startingPlayers)
        {
            if (ctx.Guild == null)
            {
                await StartNewMPGameAsync(ctx, ctx.User, ctx.Client.CurrentUser);
            }
            else
            {
                await StartNewMPGameAsync(ctx, new[] { ctx.Member }.Concatenate(startingPlayers));
            }
        }


        [Command("uno help"), Aliases("uno h", "uno rules", "uno commands"), Priority(-1), Hidden]
        [Description("Gives rules and commands for the Uno game.")]
        public async Task UnoHelp(CommandContext ctx)
        {
            var desc = MethodBase.GetCurrentMethod().GetCustomAttribute<DescriptionAttribute>();
            await ctx.RespondAsync(desc.Description);
        }


        [Command("uno join"), Aliases("uno add", "uno invite"), Priority(-1), Hidden]
        [Description("Joins an ongoing Uno game in this channel. Fails if the game is full or if there aren't enough cards to draw for you.\n" +
                 "You can also invite a bot or another user to play.")]
        [RequireGuild]
        public async Task JoinUno(CommandContext ctx, DiscordMember member = null)
        {
            bool self = false;
            if (member == null)
            {
                self = true;
                member = ctx.Member;
            }

            if (Game(ctx) == null)
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

            if (failReason == null)
            {
                await DeleteGameMessageAsync(ctx);
                await RespondGameAsync(ctx);
            }
            else
            {
                await ctx.RespondAsync($"{member.Mention} {"You ".If(self)}can't join this game: {failReason}");
            }

            if (ctx.BotCan(Permissions.ManageMessages)) await ctx.Message.DeleteAsync();
            else await ctx.AutoReactAsync(failReason == null);
        }


        [Command("uno leave"), Priority(-1), Hidden]
        [Description("Lets you leave the Uno game in this channel.")]
        [RequireGuild]
        public async Task LeaveUno(CommandContext ctx)
        {
            if (Game(ctx) == null)
            {
                await ctx.RespondAsync($"There's no Uno game in this channel! Use `{ctx.Prefix}uno` to start.");
                return;
            }
            if (!Game(ctx).UserId.Contains(ctx.User.Id))
            {
                await ctx.RespondAsync($"You're not in the game.");
                return;
            }

            await Game(ctx).RemovePlayerAsync(ctx.User);

            if (Game(ctx).UserId.Length < 2 || Game(ctx).UserId.Select(x => ctx.Guild.Members[x]).All(x => x.IsBot))
            {
                EndGame(ctx);
                await UpdateGameMessageAsync(ctx);
            }
            else
            {
                await DeleteGameMessageAsync(ctx);
                await RespondGameAsync(ctx);
            }

            await ctx.AutoReactAsync();
        }


        [Command("uno kick"), Aliases("uno remove"), Priority(-1), Hidden]
        [Description("Lets you kick another player or bot in the current Uno game.")]
        [RequireGuild]
        public async Task KickUno(CommandContext ctx, DiscordMember member = null)
        {
            if (Game(ctx) == null)
            {
                await ctx.RespondAsync($"There's no Uno game in this channel! Use `{ctx.Prefix}uno` to start.");
                return;
            }
            if (member == null)
            {
                await ctx.RespondAsync($"You must specify a user to kick from the game.");
                return;
            }
            if (!Game(ctx).UserId.Contains(member.Id))
            {
                await ctx.RespondAsync($"That user is not in the game.");
                return;
            }
            if (!member.IsBot && (Game(ctx).UserId[Game(ctx).Turn] != member.Id || (DateTime.Now - Game(ctx).LastPlayed) < TimeSpan.FromMinutes(1)))
            {
                await ctx.RespondAsync("To remove another user they must be inactive for at least 1 minute during their turn.");
            }

            await Game(ctx).RemovePlayerAsync(member);

            if (Game(ctx).UserId.Length < 2 || Game(ctx).UserId.Select(x => ctx.Guild.Members[x]).All(x => x.IsBot))
            {
                EndGame(ctx);
                await UpdateGameMessageAsync(ctx);
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
