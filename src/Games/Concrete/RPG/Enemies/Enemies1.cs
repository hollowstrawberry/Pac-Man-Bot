using PacManBot.Games.Concrete.RPG.Buffs;

namespace PacManBot.Games.Concrete.RPG.Enemies
{
    public class Slime : Enemy
    {
        public override string Name => "Green Slime";

        public override void SetStats()
        {
            Level = 1;
            ExpYield = 1;
            MaxLife = 10;
            Damage = 1;
            DamageType = DamageType.Blunt;
        }
    }


    public class BlueSlime : Enemy
    {
        public override string Name => "Blue Slime";

        public override void SetStats()
        {
            Level = 1;
            ExpYield = 2;
            MaxLife = 12;
            Damage = 2;
            DamageType = DamageType.Blunt;
        }
    }


    public class Flop : Enemy
    {
        public override string Name => "Flopper";

        public override void SetStats()
        {
            Level = 2;
            ExpYield = 2;
            MaxLife = 13;
            Damage = 2;
            CritChance = 0.5;
            DamageType = DamageType.Blunt;
            DamageResistance[DamageType.Cutting] = -0.2;
        }
    }


    public class Skeleton : Enemy
    {
        public override string Name => "Skellington";

        public override void SetStats()
        {
            Level = 3;
            ExpYield = 3;
            MaxLife = 25;
            Damage = 3;
            Defense = 1;
            CritChance = 0.2;
            DamageType = DamageType.Cutting;
            DamageResistance[DamageType.Pierce] = 0.2;
        }
    }


    public class RainElemental : Enemy
    {
        public override string Name => "Rain Elemental";

        public override void SetStats()
        {
            Level = 5;
            ExpYield = 4;
            MaxLife = 40;
            Damage = 5;
            Defense = 2;
            DamageType = DamageType.Magic;
            MagicType = MagicType.Water;
            MagicResistance[MagicType.Water] = 1;
            MagicResistance[MagicType.Fire] = 0.5;
        }

        public override string Attack(Entity target)
        {
            string msg = target.Buffs.ContainsKey(nameof(Wet)) ? "" : $"{target} is now wet.";
            target.Buffs[nameof(Wet)] = 5;
            return base.Attack(target) + msg;
        }
    }
}
