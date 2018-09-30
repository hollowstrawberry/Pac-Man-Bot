using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Net;
using Discord.Commands;
using PacManBot.Games;
using PacManBot.Games.Concrete.RPG;
using PacManBot.Constants;
using PacManBot.Extensions;

namespace PacManBot.Commands.Modules
{
    partial class MoreGamesModule
    {
        [Command("rpg"), Remarks("Play a generic RPG")]
        [Summary("This is a generic RPG. Alpha stage." +
            "\n\n**__Commands:__**" +
            "\n**{prefix}rpg battle** - Start a new battle or resend the current battle." +
            "\n**{prefix}rpg profile** - Check your hero." +
            "\n**{prefix}rpg equip [name]** - Equip a weapon in your inventory." +
            "\n**{prefix}rpg heal** - Refill your HP once every 10 minutes and once per battle.")]
        [BetterRequireBotPermission(ChannelPermission.EmbedLinks | ChannelPermission.AddReactions)]
        public async Task GenericRpg(string command = "", [Remainder]string args = "")
        {
            var game = Games.GetForUser<RpgGame>(Context.User.Id);
            if (game == null)
            {
                game = new RpgGame(Context.User.Username, Context.User.Id, Services);
                Games.Add(game);
            }

            switch (command)
            {
                case "fight":
                case "battle":
                    if (game.State != State.Active)
                    {
                        game.StartFight();
                        Games.Save(game);
                    }

                    var old = await game.GetMessage();
                    if (old != null)
                    {
                        try { await old.DeleteAsync(); }
                        catch (HttpException) { }
                    }

                    var message = await ReplyAsync(game.Fight());
                    game.ChannelId = Context.Channel.Id;
                    game.MessageId = message.Id;

                    var emotes = RpgGame.EmoteNumberInputs.Take(game.enemies.Count).Concat(RpgGame.EmoteOtherInputs);
                    try
                    {
                        foreach (var emote in emotes)
                        {
                            await message.AddReactionAsync((IEmote)emote.ToEmote() ?? emote.ToEmoji(), DefaultOptions);
                        }
                    }
                    catch { }

                    break;

                case "equip":
                case "weapon":
                    var item = game.player.inventory.Select(x => x.GetWeapon()).FirstOrDefault(x => x.Equals(args));
                    if (item == null) await ReplyAsync("That weapon is not in your inventory.");
                    else
                    {
                        game.player.EquipWeapon(item.Key);
                        Games.Save(game);
                        await AutoReactAsync();
                    }
                    break;

                case "heal":
                case "potion":
                case "hp":
                    if (game.lastHeal > game.lastBattle && game.State == State.Active)
                    {
                        await ReplyAsync($"{CustomEmoji.Cross} You already healed during this battle.");
                        return;
                    }

                    var timeLeft = TimeSpan.FromMinutes(10) - (DateTime.Now - game.lastHeal);
                    if (timeLeft > TimeSpan.Zero)
                    {
                        await ReplyAsync($"{CustomEmoji.Cross} You can heal again in {timeLeft.Humanized()}");
                        return;
                    }

                    game.lastHeal = DateTime.Now;
                    game.player.Life += 50;
                    await AutoReactAsync();
                    break;

                case "profile":
                case "stats":
                case "skills":
                case "me":
                    await ReplyAsync(game.player.Profile());
                    break;

                case "help":
                    await ReplyAsync(Help.MakeHelp("rpg"));
                    break;

                default:
                    await ReplyAsync($"Unknown RPG command. Do `{Prefix}rpg help` for help.");
                    break;
            }
        }
    }
}
