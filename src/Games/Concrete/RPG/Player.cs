using System;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Discord;
using PacManBot.Constants;
using PacManBot.Extensions;

namespace PacManBot.Games.Concrete.RPG
{
    /// <summary>
    /// A player fighting entity in the RPG. Contains most information about the user's savefile.
    /// </summary>
    [DataContract]
    public class Player : Entity
    {
        /// <summary>The player's name.</summary>
        public override string Name => name;

        /// <summary>The experience required to advance to the next level.</summary>
        public int NextLevelExp => Level == 1 ? 5 : 5 * (Level - 1);

        [DataMember] private string name;

        /// <summary>The player's current level.</summary>
        [DataMember] public override int Level { get; set; }
        /// <summary>The current experience in the current level.</summary>
        [DataMember] public int experience;
        /// <summary>The key of the weapon the player is holding.</summary>
        [DataMember] public string weapon;
        /// <summary>Profile embed color.</summary>
        [DataMember] public Color color = Color.Blue;
        /// <summary>Contains the keys of the items in the player's inventory.</summary>
        [DataMember] public List<string> inventory = new List<string>(20);


        private Player() { }

        public Player(string name) : base()
        {
            SetName(name);
            MaxLife = 50;
            Life = MaxLife;
            EquipWeapon(nameof(Weapons.Fists));

            inventory = new List<string>
            {
                nameof(Weapons.Stick),
            };
        }


        /// <summary>Safely equips a new weapon.</summary>
        public void EquipWeapon(string newWeapon)
        {
            if (weapon != null)
            {
                weapon.GetWeapon().UnequipEffects(this);
                if (weapon != nameof(Weapons.Fists)) inventory.Add(weapon);
            }

            newWeapon.GetWeapon().EquipEffects(this);
            inventory.Remove(newWeapon);
            weapon = newWeapon;
        }


        public override string Attack(Entity target)
        {
            string effectMessage = weapon.GetWeapon().AttackEffects(this, target);
            return base.Attack(target) + effectMessage;
        }


        /// <summary> Safely sets the player's name. </summary>
        public void SetName(string name)
        {
            // Here I'd do some validation eventually
            this.name = name.Truncate(32);
        }


        /// <summary>Levels up the player, increasing stats.</summary>
        public string LevelUp()
        {
            experience -= NextLevelExp;
            Level++;

            var boosts = new List<string>(5);

            MaxLife += 5;
            Life += 5;
            boosts.Add("+5 HP");
            if (Level % 2 == 0)
            {
                Damage += 1;
                boosts.Add("+1 damage");
            }
            else
            {
                Defense += 1;
                boosts.Add("+1 defense");
            }

            switch (Level)
            {
                case 3:
                    inventory.AddRange(new[] { nameof(Weapons.Shortsword), nameof(Weapons.Dagger) });
                    boosts.Add("**new weapons!**");
                    break;
                case 5:
                    inventory.AddRange(new[] { nameof(Weapons.Mace), nameof(Weapons.FireScroll) });
                    boosts.Add("**new weapons!**");
                    break;
                case 8:
                    inventory.Add(nameof(Weapons.Bow));
                    boosts.Add("**new weapon!**");
                    break;
                case 10:
                    inventory.Add(nameof(Weapons.ForestSword));
                    boosts.Add("**new weapon!**");
                    break;
                case 11:
                    inventory.Add(nameof(Weapons.Shield));
                    boosts.Add("**new weapon!**");
                    break;
                case 12:
                    inventory.Add(nameof(Weapons.SimpleSpell));
                    boosts.Add("**new weapon!**");
                    break;
            }

            return boosts.JoinString(", ");
        }


        public EmbedBuilder Profile()
        {
            var embed = new EmbedBuilder
            {
                Title = $"{Name}'s Profile",
                Color = color,
            };

            string statsDesc =
                $"**Level {Level}**  (`{experience}/{NextLevelExp} EXP`)\n" +
                $"HP: `{Life}/{MaxLife}`\nDefense: `{Defense}`";
            embed.AddField("Stats", statsDesc, true);

            var wp = weapon.GetWeapon();
            string weaponDesc = $"**[{wp.Name}]**\n`{Damage}` {wp.Type}{$"/{wp.Magic}".If(wp.Magic != MagicType.None)} damage"
                              + $"\n`{(CritChance * 100).Round()}%` critical hit chance"
                              + $"\n*\"{wp.Description}\"*";

            embed.AddField("Weapon", weaponDesc, true);

            var inv = inventory.Select(x => x.GetItem().Name).JoinString(", ");
            embed.AddField("Inventory", inv == "" ? "*Empty*" : inv);

            return embed;
        }
    }
}
