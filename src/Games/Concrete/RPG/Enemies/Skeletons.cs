using PacManBot.Games.Concrete.RPG.Buffs;
using PacManBot.Extensions;

namespace PacManBot.Games.Concrete.RPG.Enemies
{
    public class Skeleton : Enemy
    {
        public override string Name => "Skeleton";

        public override void SetStats()
        {
            Level = 3;
            ExpYield = 4;
            MaxLife = 26;
            Damage = 5;
            Defense = 1;
            CritChance = 0.2;
            DamageType = DamageType.Pierce;
            DamageResistance[DamageType.Pierce] = 0.2;
        }
    }


    public class Skeleton2 : Enemy
    {
        public override string Name => "Skellington";

        public override void SetStats()
        {
            Level = 8;
            ExpYield = 8;
            MaxLife = 42;
            Damage = 8;
            Defense = 2;
            CritChance = 0.2;
            DamageType = DamageType.Cutting;
            DamageResistance[DamageType.Pierce] = 0.2;
        }
    }


    public class Skeleton3 : Enemy
    {
        public override string Name => "Spookington";

        public override void SetStats()
        {
            Level = 11;
            ExpYield = 10;
            MaxLife = 37;
            Damage = 16;
            Defense = -2;
            CritChance = 0.1;
            DamageType = DamageType.Pierce;
            DamageResistance[DamageType.Pierce] = -0.2;
        }
    }


    public class SkeletonKing : Enemy
    {
        public override string Name => "Skingeton";

        public override void SetStats()
        {
            Level = 20;
            ExpYield = 15;
            MaxLife = 100;
            Damage = 30;
            Defense = 6;
            CritChance = 0.2;
            DamageType = DamageType.Blunt;
            DamageResistance[DamageType.Magic] = 0.15;
            DamageResistance[DamageType.Blunt] = 0.15;
        }
    }
}
