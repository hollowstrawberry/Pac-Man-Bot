using System.Runtime.Serialization;
using PacManBot.Games.Concrete.RPG.Buffs;
using PacManBot.Extensions;

namespace PacManBot.Games.Concrete.RPG.Enemies
{
    public class Skeleton : Enemy
    {
        public override string Name => "Skeleton";
        public override string Description => "";

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
        public override string Description => "A skeleton's wacky brother.";

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
        public override string Description => "Be careful, it's spooky!";

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
        public override string Name => "Swoleton";
        public override string Description => "Milk makes your bones strong!";

        [DataMember] private bool milk = false;

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
        public override string Name => "Skeleton King";
        public override string Description => "He's actually just a count, but don't tell him.";

        public override void SetStats()
        {
            Level = 20;
            ExpYield = 10;
            MaxLife = 100;
            Damage = 25;
            Defense = 2;
            CritChance = 0.15;
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
                target.AddBuff(nameof(Vulnerable), 2);
                target.AddBuff(nameof(Wet), 2);
            }
            return base.Attack(target) + msg;
        }
    }


    public class Skeleton5 : Enemy
    {
        public override string Name => "Swingeton";
        public override string Description => "Its dance is unpredictable.";

        public override void SetStats()
        {
            Level = 30;
            ExpYield = 16;
            MaxLife = 74;
            Defense = 6;
            Damage = 999;
            CritChance = 0;
            DamageType = DamageType.Cutting;
            DamageResistance[DamageType.Cutting] = 0.1;
        }

        public override string Attack(Entity target)
        {
            Damage = Bot.Random.Next(18, 60);
            string msg = base.Attack(target);
            Damage = 999;
            return msg;
        }
    }
}
