
namespace PacManBot.Games.Concrete.RPG
{
    public abstract class Weapon : Equipment
    {
        public abstract int Damage { get; }
        public abstract double CritChance { get; }
        public abstract DamageType Type { get; }
        public virtual MagicType Magic => MagicType.None;


        public virtual string AttackEffects(Player wielder, Entity target) => "";


        public override void EquipEffects(Player player)
        {
            player.Damage += Damage;
            player.CritChance += CritChance;
            player.DamageType = Type;
            player.MagicType = Magic;
        }
    }
}
