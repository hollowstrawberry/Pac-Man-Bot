using System;
using System.Collections.Generic;
using PacManBot.Games.Concrete.RPG.Buffs;

namespace PacManBot.Games.Concrete.RPG.Enemies
{
    public class Slime : Enemy
    {
        public override string Name => "Green Slime";

        public override void SetDefaults()
        {
            Level = 1;
            MaxLife = 15;
            Damage = 3;
            DamageType = DamageType.Blunt;
        }
    }


    public class BlueSlime : Enemy
    {
        public override string Name => "Blue Slime";

        public override void SetDefaults()
        {
            Level = 1;
            MaxLife = 20;
            Damage = 5;
            DamageType = DamageType.Blunt;
        }
    }


    public class Skeleton : Enemy
    {
        public override string Name => "Skellington";

        public override void SetDefaults()
        {
            Level = 2;
            MaxLife = 30;
            Damage = 5;
            CritChance = 0.1;
            DamageType = DamageType.Cutting;
            DamageResistance[DamageType.Cutting] = 0.2;
        }
    }


    public class RainElemental : Enemy
    {
        public override string Name => "Rain Elemental";

        public override void SetDefaults()
        {
            Level = 3;
            MaxLife = 30;
            Damage = 8;
            DamageType = DamageType.Magic;
            MagicType = MagicType.Water;
            MagicResistance[MagicType.Water] = 1;
        }

        public override string Attack(Entity target)
        {
            string msg = target.Buffs.ContainsKey(nameof(Wet)) ? "" : $"{target} is now wet.";
            target.Buffs[nameof(Wet)] = 5;
            return base.Attack(target) + msg;
        }
    }
}
