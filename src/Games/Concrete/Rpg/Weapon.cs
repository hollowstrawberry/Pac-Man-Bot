
namespace PacManBot.Games.Concrete.Rpg
{
    /// <summary>
    /// An item that can be equipped in the weapon slot and used to attack.
    /// </summary>
    public abstract class Weapon : Equipment
    {
        /// <summary>Base damage of this weapon type.</summary>
        public abstract int Damage { get; }
        /// <summary>Base critical hit chance of this weapon type.</summary>
        public abstract double CritChance { get; }
        /// <summary>Damage type of this weapon type.</summary>
        public abstract DamageType Type { get; }
        /// <summary>Damage type of this weapon type.</summary>
        public virtual MagicType Magic => MagicType.Magicless;


        /// <summary>Additional effects this weapon has when used.</summary>
        public virtual string AttackEffects(RpgPlayer wielder, Entity target) => "";


        public override void EquipEffects(RpgPlayer player)
        {
            player.Damage += Damage;
            player.CritChance += CritChance;
            player.DamageType = Type;
            player.MagicType = Magic;
        }
    }
}
