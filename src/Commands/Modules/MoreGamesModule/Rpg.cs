using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using PacManBot.Extensions;
using PacManBot.Games.Concrete.RPG;

namespace PacManBot.Commands.Modules
{
    partial class MoreGamesModule
    {
        [Command("rpg"), Remarks("Play a generic RPG")]
        [RequireBotPermission(ChannelPermission.EmbedLinks)]
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
                case "attack":
                    if (game.enemy == null) game.StartFight();
                    await ReplyAsync(game.FightTurn());
                    break;

                case "equip":
                case "weapon":
                    var item = game.player.inventory.Select(x => x.GetItem()).FirstOrDefault(x => x.Equals(args));
                    if (item == null) await ReplyAsync("That item is not in your inventory.");
                    else
                    {
                        game.player.EquipWeapon(item.Key);
                        await ReplyAsync(game.Profile());
                    }
                    break;

                default:
                    await ReplyAsync(game.Profile());
                    break;
            }

            Games.Save(game);
        }
    }
}
