using System;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.Serialization;
using PacManBot.Extensions;

namespace PacManBot.Games.Concrete.RPG
{
    [DataContract]
    public class Player : Entity
    {
        public override string Name => name;

        [DataMember] public string name;
        [DataMember] public string weapon;
        [DataMember] public List<string> inventory = new List<string>(20);


        private Player() { }

        public Player(string name) : base()
        {
            this.name = name;
            MaxLife = 50;
            Life = MaxLife;
            EquipWeapon(nameof(Weapons.Fists));
            inventory = Extensions.ItemTypes.Keys.ToList();
        }


        public void EquipWeapon(string newWeapon)
        {
            if (weapon != null)
            {
                weapon.Weapon().UnequipEffects(this);
                if (weapon != nameof(Weapons.Fists)) inventory.Add(weapon);
            }

            newWeapon.Weapon().EquipEffects(this);
            inventory.Remove(newWeapon);
            weapon = newWeapon;
        }


        public override string Attack(Entity target)
        {
            string effectMessage = weapon.Weapon().AttackEffects(this, target);
            return base.Attack(target) + effectMessage;
        }
    }
}
