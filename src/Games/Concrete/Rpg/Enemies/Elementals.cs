using System.Linq;
using System.Collections.Generic;
using System.Runtime.Serialization;
using PacManBot.Games.Concrete.Rpg.Buffs;
using PacManBot.Utils;
using PacManBot.Extensions;

namespace PacManBot.Games.Concrete.Rpg.Enemies
{
    public class WaterElemental1 : Enemy
    {
        public override string Name => "Drip Elemental";
        public override string Description => "A little wet, but mostly harmless.";
        public override int Level => 5;
        public override int ExpYield => 4;
        public override int BaseDamage => 6;
        public override int BaseDefense => 1;
        public override double BaseCritChance => 0.07;

        public override void SetStats()
        {
            MaxLife = 35;
            DamageType = DamageType.Magic;
            MagicType = MagicType.Water;
            MagicResistance[MagicType.Water] = 0.5;
            MagicResistance[MagicType.Fire] = 0.25;
        }

        public override string Attack(Entity target)
        {
            string msg = target.HasBuff(nameof(Wet)) ? "" : $"{target} got wet.";
            target.AddBuff(nameof(Wet), 5);
            return base.Attack(target) + msg;
        }
    }


    public class EarthElemental1 : Enemy
    {
        public override string Name => "Dirt Elemental";
        public override string Description => "Pretty tough even for its small size.";
        public override int Level => 6;
        public override int ExpYield => 4;
        public override int BaseDamage => 8;
        public override int BaseDefense => 3;
        public override double BaseCritChance => 0.1;

        public override void SetStats()
        {
            MaxLife = 40;
            DamageType = DamageType.Blunt;
            MagicType = MagicType.Earth;
            MagicResistance[MagicType.Earth] = 0.5;
        }
    }


    public class FireElemental1 : Enemy
    {
        public override string Name => "Spark Elemental";
        public override string Description => "Time to turn up the heat.";
        public override int Level => 8;
        public override int ExpYield => 6;
        public override int BaseDamage => 11;
        public override int BaseDefense => 1;
        public override double BaseCritChance => 0.05;

        public override void SetStats()
        {
            MaxLife = 36;
            DamageType = DamageType.Magic;
            MagicType = MagicType.Fire;
            MagicResistance[MagicType.Fire] = 0.5;
            MagicResistance[MagicType.Water] = -0.5;
        }

        public override string Attack(Entity target)
        {
            string msg = "";
            if (!target.HasBuff(nameof(Burn)))
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
        public override int Level => 10;
        public override int ExpYield => 8;
        public override int BaseDamage => 10;
        public override int BaseDefense => 2;
        public override double BaseCritChance => 0.01;

        public override void SetStats()
        {
            MaxLife = 45;
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
        public override int Level => 22;
        public override int ExpYield => 12;
        public override int BaseDamage => 18;
        public override int BaseDefense => 5;
        public override double BaseCritChance => 0.05;

        [DataMember] int rain = 0;

        public override void SetStats()
        {
            MaxLife = 80;
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
        public override int Level => 25;
        public override int ExpYield => 14;
        public override int BaseDamage => 21;
        public override int BaseDefense => 6;
        public override double BaseCritChance => 0.05;

        public override void SetStats()
        {
            MaxLife = 90;
            DamageType = DamageType.Magic;
            MagicType = MagicType.Earth;
            MagicResistance[MagicType.Earth] = 0.75;
            MagicResistance[MagicType.Fire] = -0.5;
        }

        public override string Attack(Entity target)
        {
            int heal = Bot.Random.Next(3, 9);
            string msg = Life == MaxLife ? "" : $"\n{Name}'s roots recover {heal} HP.";
            Life += heal;
            return base.Attack(target) + msg;
        }
    }


    public class AirElemental2 : Enemy
    {
        public override string Name => "Thunder Elemental";
        public override string Description => "The electricity is overwhelming.";
        public override int Level => 28;
        public override int ExpYield => 16;
        public override int BaseDamage => 12;
        public override int BaseDefense => 7;
        public override double BaseCritChance => 0.25;

        public override void SetStats()
        {
            MaxLife = 70;
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
                int dmg = target.Hit(AttackFormula(Damage, crit), DamageType, MagicType);

                attacks[i] = $"{dmg}" + " (!)".If(crit);
            }

            return $"{this} attacks {target} for {attacks[0]}, {attacks[1]}, and {attacks[2]} damage!";
        }
    }


    public class FireElemental2 : Enemy
    {
        public override string Name => "Lava Elemental";
        public override string Description => "Hotter than my new mixtape.";
        public override int Level => 32;
        public override int ExpYield => 18;
        public override int BaseDamage => 30;
        public override int BaseDefense => 12;
        public override double BaseCritChance => 0.05;

        public override void SetStats()
        {
            MaxLife = 90;
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
        public override string Description => "Master of the element...als.";
        public override int Level => 50;
        public override int ExpYield => 25;
        public override int BaseDamage => 160;
        public override int BaseDefense => 15;
        public override double BaseCritChance => 0.01;

        public override void SetStats()
        {
            MaxLife = 200;
            DamageType = DamageType.Magic;
            DamageResistance[DamageType.Magic] = 0.6;
        }

        public override string Attack(Entity target)
        {
            var attacks = new List<string>(4);
            foreach (var element in EnumTraits<MagicType>.Values.Skip(1))
            {
                bool crit = Bot.Random.NextDouble() < CritChance;
                int dmg = AttackFormula(Damage / 4, crit);
                attacks.Add($"{target.Hit(dmg, DamageType, element)}{"(!)".If(crit)}");
            }

            return $"{Name} attacks {target} with all four elements! {attacks.JoinString(", ")}.";
        }
    }
}
