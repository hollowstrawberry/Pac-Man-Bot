using PacManBot.Games.Concrete.RPG.Buffs;
using PacManBot.Extensions;

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
            MaxLife = 18;
            Damage = 2;
            CritChance = 0.5;
            DamageType = DamageType.Blunt;
            DamageResistance[DamageType.Cutting] = -0.2;
        }
    }


    public class Rat : Enemy
    {
        public override string Name => "Feral rat";

        public override void SetStats()
        {
            Level = 3;
            ExpYield = 3;
            MaxLife = 30;
            Damage = 5;
            Defense = 0;
            CritChance = 0.08;
            DamageType = DamageType.Cutting;
            DamageResistance[DamageType.Magic] = -0.3;
        }
    }

    
    public class Bear : Enemy
    {
        public override string Name => "Bear";

        public override void SetStats()
        {
            Level = 9;
            ExpYield = 10;
            MaxLife = 70;
            Damage = 9;
            Defense = 4;
            CritChance = 0.01;
            DamageType = DamageType.Cutting;
            DamageResistance[DamageType.Blunt] = 0.2;
        }
    }
}
