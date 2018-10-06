using System;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.Serialization;
using PacManBot.Extensions;

namespace PacManBot.Games.Concrete.Rpg
{
    /// <summary>
    /// A fighting entity, player or non-player.
    /// </summary>
    [DataContract]
    public abstract class Entity
    {
        [DataMember] private int internalLife;

        /// <summary>
        /// HP, the entity is dead when it reaches 0. 
        /// It's automatically clamped between 0 and <see cref="MaxLife"/>
        /// </summary>
        public virtual int Life
        {
            get => internalLife;
            set => internalLife = Math.Clamp(value, 0, MaxLife);
        }

        /// <summary>The visible name of this entity.</summary>
        public abstract string Name { get; }
        /// <summary>The maximum <see cref="Life"/> this entity can have at any point.</summary>
        [DataMember] public int MaxLife { get; set; }
        /// <summary>The current calculated damage of this entity.</summary>
        public int Damage { get; set; }
        /// <summary>The current calculated defense of this entity.</summary>
        public int Defense { get; set; }
        /// <summary>The current calculated damage multiplier this entity is affected by.</summary>
        public double DamageMult { get; set; } = 1;
        /// <summary>The current calculated defense multiplier this entity is affected by.</summary>
        public double DefenseMult { get; set; } = 1;
        /// <summary>The current critical hit chance of this entity.</summary>
        public double CritChance { get; set; }
        /// <summary>The damage type inflicted by this entity when attacking.</summary>
        [DataMember] public DamageType DamageType { get; set; }
        /// <summary>The magic damage type inflicted by this entity when attacking.</summary>
        [DataMember] public MagicType MagicType { get; set; }

        /// <summary>All buffs held by this entity.</summary>
        [DataMember] public List<Buff> Buffs { get; }
        /// <summary>The current typed damage boosts this entity is affected by.</summary>
        [DataMember] public Dictionary<DamageType, int> DamageBoost { get; set; }
        /// <summary>The current typed damage boosts this entity is affected by.</summary>
        [DataMember] public Dictionary<MagicType, int> MagicBoost { get; set; }
        /// <summary>The current typed damage reduction this entity has as a percentage.</summary>
        [DataMember] public Dictionary<DamageType, double> DamageResistance { get; set; }
        /// <summary>The current typed damage reduction this entity has as a percentage.</summary>
        [DataMember] public Dictionary<MagicType, double> MagicResistance { get; set; }


        public override string ToString() => Name;


        protected Entity()
        {
            Buffs = new List<Buff>(5);
            DamageBoost = new Dictionary<DamageType, int>(4);
            MagicBoost = new Dictionary<MagicType, int>(4);
            DamageResistance = new Dictionary<DamageType, double>(4);
            MagicResistance = new Dictionary<MagicType, double>(5);
        }


        /// <summary>Updates the entity's stats, affected by all active stat change sources.</summary>
        public virtual void UpdateStats()
        {
            foreach (var buff in Buffs)
            {
                buff.BuffEffects(this);
            }

            if (DamageBoost.TryGetValue(DamageType, out var dmgB)) Damage += dmgB;
            if (MagicBoost.TryGetValue(MagicType, out var magicB)) Damage += magicB;

            Damage = (Damage * DamageMult).Round();
            Defense = (Defense * DefenseMult).Round();
        }



        /// <summary>Performs a tick on all active buffs of this entity, and returns all buff tick messages.</summary>
        public virtual string TickBuffs()
        {
            var msg = new StringBuilder();

            foreach (var buff in Buffs.ToList())
            {
                msg.AppendLine(buff.TickEffects(this));
                if (--buff.timeLeft == 0) Buffs.Remove(buff);
            }

            return msg.ToString();
        }


        /// <summary>
        /// Deals damage to this entity applying damage reduction calculations and returns the damage received.
        /// </summary>
        public virtual int Hit(int damage, DamageType type, MagicType magic)
        {
            double modified = (damage - Defense)
                * (1 - DamageResistance.GetValueOrDefault(type, 0) - MagicResistance.GetValueOrDefault(magic, 0));

            int final = Math.Max(0, modified.Ceiling());
            Life -= final;
            return final;
        }


        /// <summary>Deals damage to another entity applying damage calculations, and returns an attack message.</summary>
        public virtual string Attack(Entity target)
        {
            bool crit = Bot.Random.NextDouble() < CritChance;
            int dealt = target.Hit(AttackFormula(Damage, crit), DamageType, MagicType);

            return $"{this} dealt {dealt} damage to {target}. {"Critical hit!".If(crit)} ";
        }


        /// <summary>Generates an attack using damage calculations.</summary>
        public static int AttackFormula(int baseDmg, bool crit)
        {
            return baseDmg <= 0 ? 0 : (baseDmg * (crit ? 2 : 1) * Bot.Random.NextDouble(0.85, 1.15)).Ceiling();
        }


        /// <summary>Returns whether this entity contains a buff of the given type.</summary>
        public bool HasBuff(string buff)
        {
            return Buffs.Any(x => x.Key == buff);
        }

        /// <summary>Safely adds a buff to this entity.</summary>
        public void AddBuff(Buff buff, int duration)
        {
            Buffs.Add(buff);
            UpdateStats();
        }

        /// <summary>Safely adds a buff to this entity.</summary>
        public void AddBuff(string buff, int duration)
        {
            Buffs.Add(buff.GetBuff().MakeNew(duration));
            UpdateStats();
        }
    }
}
