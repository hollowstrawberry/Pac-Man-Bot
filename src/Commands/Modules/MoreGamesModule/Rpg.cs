using System.Threading.Tasks;
using Discord;
using Discord.Commands;
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
                    if (game.player.inventory.Contains(args))
                    {
                        game.player.EquipWeapon(args);
                        await ReplyAsync(game.Profile());
                    }
                    else await ReplyAsync("That item is not in your inventory.");
                    break;

                default:
                    await ReplyAsync(game.Profile());
                    break;
            }

            //Games.Save(game);
        }
    }
}
