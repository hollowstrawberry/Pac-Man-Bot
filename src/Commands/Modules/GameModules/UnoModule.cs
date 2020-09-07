using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using PacManBot.Extensions;
using PacManBot.Games.Concrete;

namespace PacManBot.Commands.Modules.GameModules
{
    [Name(ModuleNames.Games), Remarks("3")]
    public class UnoModule : MultiplayerGameModule<UnoGame>
    {
        [Command("uno"), Parameters("[players]"), Priority(3)]
        [Remarks("Play Uno with up to 10 friends and bots")]
        [ExampleUsage("uno\nuno @Pac-Man#3944")]
        [Summary("__Tip__: Switching back and forth with DMs to see your cards can be tiresome, " +
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
        public async Task StartUno(params SocketGuildUser[] startingPlayers)
        {
            if (Context.Guild == null)
            {
                await RunGameAsync(Context.User, Context.Client.CurrentUser);
            }
            else
            {
                await RunGameAsync(new[] { Context.User as SocketGuildUser }.Concatenate(startingPlayers));
            }
        }


        [Command("uno help"), Alias("uno h", "uno rules", "uno commands"), Priority(-1), HideHelp]
        [Summary("Gives rules and commands for the Uno game.")]
        public async Task UnoHelp() => await ReplyAsync(Commands.GetCommandHelp("uno", Context));


        [Command("uno join"), Alias("uno add", "uno invite"), Priority(-1), HideHelp]
        [Summary("Joins an ongoing Uno game in this channel. Fails if the game is full or if there aren't enough cards to draw for you.\n" +
                 "You can also invite a bot or another user to play.")]
        [RequireContext(ContextType.Guild)]
        public async Task JoinUno(SocketGuildUser user = null)
        {
            bool self = false;
            if (user == null)
            {
                self = true;
                user = Context.User as SocketGuildUser;
            }

            if (Game == null)
            {
                await ReplyAsync($"There's no Uno game in this channel! Use `{Context.Prefix}uno` to start.");
                return;
            }
            if (Game.UserId.Contains(user.Id))
            {
                await ReplyAsync($"{(self ? "You're" : "They're")} already playing!");
                return;
            }
            if (!self && !user.IsBot)
            {
                await ReplyAsync($"{user.Mention} You're being invited to play {Game.GameName}.\nDo `{Context.Prefix}uno join` to join.");
                return;
            }

            string failReason = await Game.TryAddPlayerAsync(user);

            if (failReason == null)
            {
                await DeleteGameMessageAsync();
                await ReplyGameAsync();
            }
            else
            {
                await ReplyAsync($"{user.Mention} {"You ".If(self)}can't join this game: {failReason}");
            }

            if (Context.BotCan(ChannelPermission.ManageMessages)) await Context.Message.DeleteAsync(DefaultOptions);
            else await AutoReactAsync(failReason == null);
        }


        [Command("uno leave"), Priority(-1), HideHelp]
        [Summary("Lets you leave the Uno game in this channel.")]
        [RequireContext(ContextType.Guild)]
        public async Task LeaveUno()
        {
            if (Game == null)
            {
                await ReplyAsync($"There's no Uno game in this channel! Use `{Context.Prefix}uno` to start.");
                return;
            }
            if (!Game.UserId.Contains(Context.User.Id))
            {
                await ReplyAsync($"You're not in the game.");
                return;
            }

            Game.RemovePlayer(Context.User);

            if (Game.AllBots || Game.UserId.Length < 2)
            {
                EndGame();
                await UpdateGameMessageAsync();
            }
            else
            {
                await DeleteGameMessageAsync();
                await ReplyGameAsync();
            }

            await AutoReactAsync();
        }


        [Command("uno kick"), Alias("uno remove"), Priority(-1), HideHelp]
        [Summary("Lets you kick another player or bot in the current Uno game.")]
        [RequireContext(ContextType.Guild)]
        public async Task KickUno(SocketGuildUser user = null)
        {
            if (Game == null)
            {
                await ReplyAsync($"There's no Uno game in this channel! Use `{Context.Prefix}uno` to start.");
                return;
            }
            if (user == null)
            {
                await ReplyAsync($"You must specify a user to kick from the game.");
                return;
            }
            if (!Game.UserId.Contains(user.Id))
            {
                await ReplyAsync($"That user is not in the game.");
                return;
            }
            if (!user.IsBot && (Game.UserId[Game.Turn] != user.Id || (DateTime.Now - Game.LastPlayed) < TimeSpan.FromMinutes(1)))
            {
                await ReplyAsync("To remove another user they must be inactive for at least 1 minute during their turn.");
            }

            Game.RemovePlayer(user);

            if (Game.AllBots || Game.UserId.Length < 2)
            {
                EndGame();
                await UpdateGameMessageAsync();
            }
            else
            {
                await DeleteGameMessageAsync();
                await ReplyGameAsync();
            }

            await AutoReactAsync();
        }
    }
}
