using System;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Discord;
using PacManBot.Constants;
using PacManBot.Extensions;

namespace PacManBot.Games.Concrete.RPG
{
    [DataContract]
    class RpgGame : ChannelGame, IUserGame, IStoreableGame
    {
        public override string GameName => "Generic RPG";
        public override int GameIndex => 9;
        public string FilenameKey => "rpg";
        public override TimeSpan Expiry => TimeSpan.FromDays(100);


        [DataMember] public Player player;
        [DataMember] public Enemy enemy;
        [DataMember] public Color ProfileColor = Colors.DarkBlack;

        [DataMember] public override ulong OwnerId { get => base.OwnerId; protected set => base.OwnerId = value; }
        [DataMember] public override DateTime LastPlayed { get => base.LastPlayed; set => base.LastPlayed = value; }


        private RpgGame() { }

        public RpgGame(string name, ulong userId, IServiceProvider services)
            : base(0, new[] { userId }, services)
        {
            player = new Player(name);
        }


        public EmbedBuilder Profile()
        {
            var embed = new EmbedBuilder
            {
                Title = $"{player.Name}'s Summary",
                Color = ProfileColor,
            };

            string statsDesc =
                $"**Level {player.Level}**  (`{player.experience}/{player.NextLevelExp} EXP`)\n" +
                $"HP: `{player.Life}/{player.MaxLife}`\nDefense: `{player.Defense}`";
            embed.AddField("Stats", statsDesc, true);

            var wp = player.weapon.GetWeapon();
            string weaponDesc = $"**[{wp.Name}]**\n`{wp.Damage}` {wp.Type} damage"
                              + $" | {wp.Magic} magic".If(wp.Magic != MagicType.None)
                              + $"\n*\"{wp.Description}\"*";
            embed.AddField("Weapon", weaponDesc, true);

            embed.AddField("Inventory", player.inventory.Select(x => x.GetItem().Name).JoinString(", "), true);

            return embed;
        }


        public void StartFight()
        {
            var enemies = Extensions.EnemyTypes.Select(x => x.Value).Where(x => x.Level <= player.Level).ToList();
            enemy = Bot.Random.Choose(enemies).MakeNew();
        }


        public EmbedBuilder FightTurn(PlayerFightAction action = PlayerFightAction.Attack)
        {
            var embed = new EmbedBuilder
            {
                Title = $"{player} vs {enemy}",
                Color = Colors.DarkBlack,
            };

            var desc = new StringBuilder();

            desc.AppendLine(player.Attack(enemy));
            string eBuffs = enemy.UpdateBuffs();
            if (eBuffs != "") desc.AppendLine(eBuffs);

            if (enemy.Life == 0) State = State.Win;
            else
            {
                desc.AppendLine(enemy.Attack(player));
                string pBuffs = player.UpdateBuffs();
                if (pBuffs != "") desc.AppendLine(pBuffs);

                if (player.Life == 0) State = State.Lose;
            }


            foreach (var ent in new Entity[] { player, enemy })
            {
                embed.AddField(ent.Name,
                    $"{ent.Life}/{ent.MaxLife} {ent.Buffs.Select(x => x.Key.GetBuff().Icon).JoinString(" ")}", true);
            }

            
            if (State == State.Win)
            {
                embed.Color = Colors.Green;
                desc.AppendLine($"You win! +{enemy.ExpYield} EXP");
                player.experience += enemy.ExpYield;
                if (player.experience >= player.NextLevelExp) desc.AppendLine($"\n⏫ Level up! {player.LevelUp()}");
                enemy = null;
            }
            else if (State == State.Lose)
            {
                embed.Color = Colors.Red;
                desc.AppendLine("You died!");
                enemy = null;
                player.Life = player.MaxLife;
            }
            State = State.Active;

            embed.Description = desc.ToString();

            return embed;
        }




        public void PostDeserialize(IServiceProvider services)
        {
            SetServices(services);
        }
    }
}
