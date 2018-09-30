using PacManBot.Extensions;

namespace PacManBot.Games.Concrete.RPG.Weapons
{
    public class Fists : Weapon
    {
        public override string Name => "Fists";
        public override string Description => "You can't really take them off,\nbut you can put something on.";
        public override int Damage => 1;
        public override double CritChance => 0;
        public override DamageType Type => DamageType.Blunt;
    }


    public class Stick : Weapon
    {
        public override string Name => "Stick";
        public override string Description => "You found this on the floor.\nWhy did you even pick it up?";
        public override int Damage => 2;
        public override double CritChance => 0.01f;
        public override DamageType Type => DamageType.Blunt;
    }


    public class Shortsword : Weapon
    {
        public override string Name => "Shortsword";
        public override string Description => "A rounded weapon for a rounded beginner.";
        public override int Damage => 4;
        public override double CritChance => 0.08f;
        public override DamageType Type => DamageType.Cutting;
    }


    public class Dagger : Weapon
    {
        public override string Name => "Dagger";
        public override string Description => "A beginner weapon likely to deal critical hits.";
        public override int Damage => 3;
        public override double CritChance => 0.2f;
        public override DamageType Type => DamageType.Pierce;
    }


    public class Mace : Weapon
    {
        public override string Name => "Mace";
        public override string Description => "A slow beginner weapon that deals decent damage.";
        public override int Damage => 5;
        public override double CritChance => 0.03f;
        public override DamageType Type => DamageType.Blunt;
    }


    public class FireScroll : Weapon
    {
        public override string Name => "Fire Scroll";
        public override string Description => "A beginner spell that may cause an extra burn.";
        public override int Damage => 4;
        public override double CritChance => 0.01f;
        public override DamageType Type => DamageType.Magic;
        public override MagicType Magic => MagicType.Fire;

        public override string AttackEffects(Player wielder, Entity target)
        {
            if (Bot.Random.OneIn(5) && !target.Buffs.ContainsKey(nameof(Buffs.Burn)))
            {
                target.AddBuff(nameof(Buffs.Burn), 4);
                return $"{target} got burned!";
            }
            return "";
        }
    }
}
