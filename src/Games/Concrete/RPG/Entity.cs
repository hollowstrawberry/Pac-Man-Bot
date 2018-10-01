using System;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.Serialization;
using PacManBot.Extensions;

namespace PacManBot.Games.Concrete.RPG
{
    /// <summary>
    /// A fighting entity in the RPG, player or non-player.
    /// </summary>
    [DataContract]
    public abstract class Entity
    {
        [DataMember] private int internalLife;

        public virtual int Life
        {
            get => internalLife;
            set => internalLife = Math.Clamp(value, 0, MaxLife);
        }

        public abstract string Name { get; }
        [DataMember] public virtual int Level { get; set; }
        [DataMember] public int MaxLife { get; set; }
        [DataMember] public int Damage { get; set; }
        [DataMember] public int Defense { get; set; }
        [DataMember] public double DamageMult { get; set; } = 1;
        [DataMember] public double DefenseMult { get; set; } = 1;
        [DataMember] public double CritChance { get; set; }
        [DataMember] public DamageType DamageType { get; set; }
        [DataMember] public MagicType MagicType { get; set; }

        [DataMember] public Dictionary<string, int> Buffs { get; }
        [DataMember] public Dictionary<DamageType, double> DamageResistance { get; set; }
        [DataMember] public Dictionary<MagicType, double> MagicResistance { get; set; }


        public override string ToString() => Name;


        protected Entity()
        {
            Level = 1;
            Buffs = new Dictionary<string, int>(5);
            DamageResistance = new Dictionary<DamageType, double>(4);
            MagicResistance = new Dictionary<MagicType, double>(5);
        }


        /// <summary>
        /// Performs a tick on all active buffs of this entity, and returns all buff tick messages.
        /// </summary>
        public virtual string UpdateBuffs()
        {
            var msg = new StringBuilder();

            foreach (var (buff, duration) in Buffs.Select(x => (x.Key, x.Value)).ToArray())
            {
                msg.AppendLine(buff.GetBuff().TickEffects(this));
                if (duration == 1)
                {
                    Buffs.Remove(buff);
                    buff.GetBuff().EndEffects(this);
                }
                else Buffs[buff] -= 1;
            }

            return msg.ToString();
        }


        /// <summary>
        /// Deals damage to this entity applying damage reduction calculations and returns the damage received.
        /// </summary>
        public virtual int Hit(int damage, DamageType type, MagicType magic)
        {
            double modified = (damage - Defense) * (1 - DamageResistance.GetValueOrDefault(type, 0) * DefenseMult)
                                                 * (1 - MagicResistance.GetValueOrDefault(magic, 0) * DefenseMult);

            int final = Math.Max(0, modified.Ceiling());
            Life -= final;
            return final;
        }


        /// <summary>
        /// Deals damage to another entity applying damage calculations, and returns an attack message.
        /// </summary>
        public virtual string Attack(Entity target)
        {
            bool crit = Bot.Random.NextDouble() < CritChance;
            int dmg = Damage <= 0 ? 0 : (Damage * DamageMult * (crit ? 2 : 1) * Bot.Random.NextDouble(0.85, 1.15)).Ceiling();
            int dealt = target.Hit(dmg, DamageType, MagicType);

            return $"{this} dealt {dealt} damage to {target}. {"Critical hit!".If(crit)} ";
        }


        /// <summary>
        /// Safely adds a buff to this entity.
        /// </summary>
        public void AddBuff(string buff, int duration)
        {
            if (!Buffs.ContainsKey(buff)) buff.GetBuff().StartEffects(this);
            Buffs[buff] = duration;
        }


        /// <summary>
        /// Safely removes a buff from this entity.
        /// </summary>
        public void RemoveBuff(string buff)
        {
            if (!Buffs.ContainsKey(buff)) return;

            Buffs.Remove(buff);
            buff.GetBuff().EndEffects(this);
        }
    }
}
