using PacManBot.Games.Concrete.RPG.Buffs;
using PacManBot.Extensions;

namespace PacManBot.Games.Concrete.RPG.Enemies
{
    public class Slime : Enemy
    {
        public override string Name => "Green Slime";
        public override string Description => "So common and weak it's boring.";

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
        public override string Description => "Almost as weak as the green variety.";

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
        public override string Description => "It flops around messily. Weird one.";

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
        public override string Name => "Feral Rat";
        public override string Description => "Just your basic rat enemy.";

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
        public override string Description => "Grrrr.";

        public override void SetStats()
        {
            Level = 9;
            ExpYield = 7;
            MaxLife = 70;
            Damage = 9;
            Defense = 4;
            CritChance = 0.01;
            DamageType = DamageType.Cutting;
            DamageResistance[DamageType.Blunt] = 0.2;
        }
    }


    public class Goblin : Enemy
    {
        public override string Name => "Goblin";
        public override string Description => "Its spear can expose your weak points.";

        public override void SetStats()
        {
            Level = 12;
            ExpYield = 8;
            MaxLife = 60;
            Damage = 10;
            Defense = 3;
            CritChance = 0.05;
            DamageType = DamageType.Pierce;
        }

        public override string Attack(Entity target)
        {
            string msg = "";
            if (!target.Buffs.ContainsKey(nameof(Vulnerable)) && Bot.Random.OneIn(3))
            {
                msg = $"{target} is now vulnerable!";
                target.AddBuff(nameof(Vulnerable), 3);
            }
            return base.Attack(target) + msg;
        }
    }


    public class Slime3 : Enemy
    {
        public override string Name => "Dark Slime";
        public override string Description => "It's so mushy you can barely damage it.";

        public override void SetStats()
        {
            Level = 14;
            ExpYield = 8;
            MaxLife = 60;
            Damage = 15;
            Defense = 5;
            CritChance = 0.05;
            DamageType = DamageType.Blunt;
            DamageResistance[DamageType.Blunt] = 0.5;
            DamageResistance[DamageType.Cutting] = 0.2;
            DamageResistance[DamageType.Pierce] = 0.5;
            DamageResistance[DamageType.Magic] = -0.25;
        }

        public override string Attack(Entity target)
        {
            string msg = "";
            if (!target.Buffs.ContainsKey(nameof(Blinded)) && Bot.Random.OneIn(3))
            {
                msg = $"{target} got slime in their eyes!";
                target.AddBuff(nameof(Blinded), 3);
            }
            return base.Attack(target) + msg;
        }
    }


    public class MechaBear : Enemy
    {
        public override string Name => "Mecha Bear";
        public override string Description => "Grrrrr BEEP grrr BOOP";

        public override void SetStats()
        {
            Level = 26;
            ExpYield = 15;
            MaxLife = 110;
            Damage = 35;
            Defense = 8;
            CritChance = 0.02;
            DamageType = DamageType.Cutting;
            DamageResistance[DamageType.Cutting] = 0.5;
        }
    }
}
