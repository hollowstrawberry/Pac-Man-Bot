using System;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.Serialization;
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
        /// <summary>Contains the keys of the items in the player's inventory.</summary>
        [DataMember] public List<string> inventory = new List<string>(20);


        private Player() { }

        public Player(string name) : base()
        {
            SetName(name);
            MaxLife = 50;
            Life = MaxLife;
            EquipWeapon(nameof(Weapons.Fists));
            inventory = Extensions.ItemTypes.Keys.ToList();
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
            MaxLife += 5;
            Life += 5;
            Damage += 1;
            Defense += 1;

            return "+5 HP, +1 damage, +1 defense";
        }




        /// <summary>
        /// Calculates the experience needed to reach a level.
        /// The difference in experience to the next level will be 5 times the level minus one, starting from level 2.
        /// </summary>
        public static int ExperienceNeededFor(int level)
        {
            return level <= 1 ? 0 : (2.5 * level * (level - 3) + 10).Floor();
        }

        /// <summary>
        /// Converts experience into a level using the inverse function of <see cref="ExperienceNeededFor(int)"/>
        /// </summary>
        public static int LevelAt(int experience)
        {
            return experience < 5 ? 1 : (0.5 * (3 + Math.Sqrt(1.6*experience - 7))).Floor();
        }
    }
}
