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
            ExpYield = 3;
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
            ExpYield = 7;
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
            ExpYield = 7;
            MaxLife = 37;
            Damage = 16;
            Defense = -2;
            CritChance = 0.1;
            DamageType = DamageType.Pierce;
            DamageResistance[DamageType.Pierce] = -0.2;
        }
    }


    public class Skeleton4 : Enemy
    {
        public override string Name => "Strongeton";
        private bool milk = false;

        public override void SetStats()
        {
            Level = 15;
            ExpYield = 9;
            MaxLife = 60;
            Damage = 12;
            Defense = 2;
            CritChance = 0.02;
            DamageType = DamageType.Blunt;
            DamageResistance[DamageType.Blunt] = 0.3;
        }

        public override string Attack(Entity target)
        {
            string msg = "";
            if (Life < MaxLife / 2 && !milk)
            {
                milk = true;
                msg = $"{Name} drank some milk and became stronger!\n";
                Damage += 4;
                Defense += 4;
                Life += MaxLife / 2;
            }

            return msg + base.Attack(target);
        }
    }


    public class SkeletonKing : Enemy
    {
        public override string Name => "Skingeton";

        public override void SetStats()
        {
            Level = 18;
            ExpYield = 10;
            MaxLife = 100;
            Damage = 30;
            Defense = 6;
            CritChance = 0.2;
            DamageType = DamageType.Blunt;
            DamageResistance[DamageType.Magic] = 0.15;
            DamageResistance[DamageType.Blunt] = 0.15;
        }

        public override string Attack(Entity target)
        {
            string msg = "";
            if (Bot.Random.OneIn(4))
            {
                msg = $"{target} is overwhelmed!";
                target.AddBuff(nameof(Burn), 2);
                target.AddBuff(nameof(Blinded), 2);
            }
            return base.Attack(target) + msg;
        }
    }
}
