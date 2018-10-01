using System.Linq;
using System.Collections.Generic;
using System.Runtime.Serialization;
using PacManBot.Games.Concrete.RPG.Buffs;
using PacManBot.Utils;
using PacManBot.Extensions;

namespace PacManBot.Games.Concrete.RPG.Enemies
{
    public class WaterElemental1 : Enemy
    {
        public override string Name => "Drip Elemental";
        public override string Description => "A little wet, but mostly harmless.";

        public override void SetStats()
        {
            Level = 5;
            ExpYield = 4;
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
            target.AddBuff(nameof(Wet), 5);
            return base.Attack(target) + msg;
        }
    }


    public class EarthElemental1 : Enemy
    {
        public override string Name => "Dirt Elemental";
        public override string Description => "Pretty tough even for its small size.";

        public override void SetStats()
        {
            Level = 6;
            ExpYield = 4;
            MaxLife = 50;
            Damage = 5;
            Defense = 4;
            CritChance = 0.05;
            DamageType = DamageType.Blunt;
            MagicType = MagicType.Earth;
            MagicResistance[MagicType.Earth] = 0.5;
        }
    }


    public class FireElemental1 : Enemy
    {
        public override string Name => "Spark Elemental";
        public override string Description => "Time to turn up the heat.";

        public override void SetStats()
        {
            Level = 8;
            ExpYield = 6;
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
                target.AddBuff(nameof(Burn), 4);
            }
            return base.Attack(target) + msg;
        }
    }


    public class AirElemental1 : Enemy
    {
        public override string Name => "Breeze Elemental";
        public override string Description => "It's so fast it might strike twice.";

        public override void SetStats()
        {
            Level = 10;
            ExpYield = 8;
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


    public class WaterElemental2 : Enemy
    {
        public override string Name => "Rain Elemental";
        public override string Description => "Reminds of cold winter days.";

        [DataMember] int rain = 0;

        public override void SetStats()
        {
            Level = 22;
            ExpYield = 12;
            MaxLife = 80;
            Damage = 22;
            Defense = 5;
            CritChance = 0.05;
            DamageType = DamageType.Magic;
            MagicType = MagicType.Water;
            MagicResistance[MagicType.Water] = 0.75;
            MagicResistance[MagicType.Fire] = 0.4;
        }

        public override string Attack(Entity target)
        {
            string msg = "";
            if (Bot.Random.OneIn(4) && rain == 0)
            {
                rain = 6;
                msg += "\nA downpour started!";
            }
            if (rain > 0)
            {
                int rainDmg = Bot.Random.Next(3, 6);
                target.Life -= rainDmg;
                msg += $"\n{target} takes {rainDmg} damage from the rain.";
                target.AddBuff(nameof(Wet), 1);
                rain--;
                if (rain == 0) msg += "\nThe downpour stopped.";
            }

            return base.Attack(target) + msg;
        }
    }


    public class EarthElemental2 : Enemy
    {
        public override string Name => "Forest Elemental";
        public override string Description => "Quite a tree-hugger, this one.";

        public override void SetStats()
        {
            Level = 25;
            ExpYield = 14;
            MaxLife = 90;
            Damage = 25;
            Defense = 6;
            CritChance = 0.05;
            DamageType = DamageType.Magic;
            MagicType = MagicType.Earth;
            MagicResistance[MagicType.Earth] = 0.75;
            MagicResistance[MagicType.Fire] = -0.5;
        }

        public override string Attack(Entity target)
        {
            int heal = Bot.Random.Next(3, 9);
            Life += heal;
            return base.Attack(target) + $"\n{Name}'s roots recover {heal} HP.";
        }
    }


    public class AirElemental2 : Enemy
    {
        public override string Name => "Thunder Elemental";
        public override string Description => "The electricity is overwhelming.";

        public override void SetStats()
        {
            Level = 28;
            ExpYield = 16;
            MaxLife = 100;
            Damage = 21;
            Defense = 7;
            CritChance = 0.2;
            DamageType = DamageType.Magic;
            MagicType = MagicType.Air;
            MagicResistance[MagicType.Air] = 0.75;
            MagicResistance[MagicType.Water] = 0.4;
        }

        public override string Attack(Entity target)
        {
            if (Bot.Random.OneIn(3)) target.AddBuff(nameof(Burn), 2);

            var attacks = new string[3];
            for (int i = 0; i < attacks.Length; i++)
            {
                bool crit = Bot.Random.NextDouble() < CritChance;
                int dmg = Damage <= 0 ? 0 : (Damage * DamageMult * (crit ? 1.5 : 1) * Bot.Random.NextDouble(0.85, 1.15)).Ceiling();
                dmg = target.Hit(dmg, DamageType, MagicType);

                attacks[i] = $"{dmg}" + " (!)".If(crit);
            }

            return $"{this} attacks {target} for {attacks[0]}, {attacks[1]}, and {attacks[2]} damage!";
        }
    }


    public class FireElemental2 : Enemy
    {
        public override string Name => "Lava Elemental";
        public override string Description => "Hotter than my new mixtape.";

        public override void SetStats()
        {
            Level = 32;
            ExpYield = 18;
            MaxLife = 90;
            Damage = 40;
            Defense = 12;
            CritChance = 0.05;
            DamageType = DamageType.Magic;
            MagicType = MagicType.Fire;
            MagicResistance[MagicType.Fire] = 0.75;
        }

        public override string Attack(Entity target)
        {
            string msg = "";
            double rand = Bot.Random.NextDouble();
            if (rand < 0.3)
            {
                msg += $"{target} got burned!";
                target.AddBuff(nameof(Burn), 4);
            }
            if (rand < 0.15)
            {
                msg += $"\n{target}'s eyes hurt from the heat!";
                target.AddBuff(nameof(Blinded), 4);
            }

            return base.Attack(target) + msg;
        }
    }


    public class AllElemental1 : Enemy
    {
        public override string Name => "Elemental Elemental";
        public override string Description => "Master of the element..als.";

        public override void SetStats()
        {
            Level = 40;
            ExpYield = 25;
            MaxLife = 200;
            Damage = 132;
            Defense = 15;
            DamageType = DamageType.Magic;
            DamageResistance[DamageType.Magic] = 0.6;
        }

        public override string Attack(Entity target)
        {
            var attacks = new List<int>(4);
            foreach (var element in EnumTraits<MagicType>.Values.Skip(1))
            {
                double dmg = (Damage / 4.0) * Bot.Random.NextDouble(0.85, 1.15);
                attacks.Add(target.Hit(dmg.Round(), DamageType, element));
            }

            return $"{Name} attacks {target} with all four elements! {attacks.JoinString(", ")}";
        }
    }
}
