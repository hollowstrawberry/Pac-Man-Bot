using PacManBot.Games.Concrete.RPG.Buffs;
using PacManBot.Extensions;

namespace PacManBot.Games.Concrete.RPG.Enemies
{
    public class DripElemental : Enemy
    {
        public override string Name => "Drip Elemental";

        public override void SetStats()
        {
            Level = 5;
            ExpYield = 5;
            MaxLife = 35;
            Damage = 5;
            Defense = 2;
            CritChance = 0.07;
            DamageType = DamageType.Magic;
            MagicType = MagicType.Water;
            MagicResistance[MagicType.Water] = 0.5;
            MagicResistance[MagicType.Fire] = 0.25;
        }

        public override string Attack(Entity target)
        {
            string msg = target.Buffs.ContainsKey(nameof(Wet)) ? "" : $"{target} got wet.";
            target.Buffs[nameof(Wet)] = 5;
            return base.Attack(target) + msg;
        }
    }


    public class DirtElemental : Enemy
    {
        public override string Name => "Dirt Elemental";

        public override void SetStats()
        {
            Level = 6;
            ExpYield = 5;
            MaxLife = 50;
            Damage = 5;
            Defense = 4;
            CritChance = 0.05;
            DamageType = DamageType.Pierce;
            MagicType = MagicType.Earth;
            MagicResistance[MagicType.Earth] = 0.5;
        }
    }


    public class SparkElemental : Enemy
    {
        public override string Name => "Spark Elemental";

        public override void SetStats()
        {
            Level = 8;
            ExpYield = 8;
            MaxLife = 30;
            Damage = 9;
            Defense = 4;
            CritChance = 0.05;
            DamageType = DamageType.Magic;
            MagicType = MagicType.Fire;
            MagicResistance[MagicType.Fire] = 0.5;
            MagicResistance[MagicType.Water] = -0.5;
        }

        public override string Attack(Entity target)
        {
            string msg = "";
            if (!target.Buffs.ContainsKey(nameof(Burn)))
            {
                msg = $"{target} got burned!";
                target.Buffs[nameof(Burn)] = 4;
            }
            return base.Attack(target) + msg;
        }
    }


    public class BreezeElemental : Enemy
    {
        public override string Name => "Breeze Elemental";

        public override void SetStats()
        {
            Level = 10;
            ExpYield = 9;
            MaxLife = 45;
            Damage = 12;
            Defense = 2;
            CritChance = 0.02;
            DamageType = DamageType.Magic;
            MagicType = MagicType.Air;
            MagicResistance[MagicType.Air] = 0.5;
        }

        public override string Attack(Entity target)
        {
            string result = base.Attack(target);
            if (Bot.Random.OneIn(3)) result += $"\n{Name} attacks a second time!\n{base.Attack(target)}";
            return result;
        }
    }
}
