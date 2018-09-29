using System;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Discord;
using PacManBot.Services;
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
                Title = $"{player.name}'s Summary",
            };

            embed.AddField("HP", $"{player.Life}/{player.MaxLife}", true);
            embed.AddField("Weapon", player.weapon.Weapon().Name, true);
            embed.AddField("Inventory", player.inventory.Select(x => x.Item().Name).JoinString(", "), true);

            return embed;
        }


        public void StartFight()
        {
            var enemies = Extensions.EnemyTypes.Select(x => x.Value).Where(x => x.Level <= player.Level).ToList();
            enemy = Bot.Random.Choose(enemies).MakeNew();
        }


        public EmbedBuilder FightTurn()
        {
            var embed = new EmbedBuilder
            {
                Title = $"{player} vs {enemy}",
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
                    $"{ent.Life}/{ent.MaxLife} {ent.Buffs.Select(x => x.Key.Buff().Icon).JoinString(" ")}", true);
            }

            
            if (State == State.Win)
            {
                embed.Color = Colors.Green;
                desc.AppendLine("You win!");
                player.Level += 1;
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
