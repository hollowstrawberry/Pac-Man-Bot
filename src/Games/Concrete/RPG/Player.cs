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
        public const int LevelCap = 20;

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
            if (Level >= LevelCap)
            {
                experience = NextLevelExp;
                return "";
            }

            experience -= NextLevelExp;
            Level++;

            var boosts = new List<string>(5);

            MaxLife += 5;
            Life = MaxLife;
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

            if (Level % 5 == 0)
            {
                CritChance += 0.01;
                boosts.Add("+1% crit chance");
            }

            if (AddLeveledWeapons()) boosts.Add("**new weapons!**");

            return boosts.JoinString(", ");
        }


        public bool AddLeveledWeapons()
        {
            var weps = new Dictionary<int, string[]>
            {
                { 3, new[] { nameof(Weapons.Shortsword), nameof(Weapons.Dagger) } },
                { 5, new[] { nameof(Weapons.Mace), nameof(Weapons.FireScroll) } },
                { 8, new[] { nameof(Weapons.Bow) } },
                { 10, new[] { nameof(Weapons.ForestSword) } },
                { 11, new[] { nameof(Weapons.Shield) } },
                { 12, new[] { nameof(Weapons.SimpleSpell) } },
            };

            bool added = false;

            foreach (var pair in weps)
            {
                if (Level >= pair.Key)
                {
                    var toAdd = pair.Value.Where(x => !inventory.Contains(x));
                    if (toAdd.Count() > 0)
                    {
                        inventory.AddRange(toAdd);
                    }
                }
            }

            return added;
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
