using System;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.Serialization;
using PacManBot.Extensions;

namespace PacManBot.Games.Concrete.RPG
{
    [DataContract]
    public abstract class Entity
    {
        private int internalLife;

        [DataMember]
        public virtual int Life
        {
            get => internalLife;
            set => internalLife = value >= 0 ? (value <= MaxLife ? value : MaxLife) : 0;
        }

        public abstract string Name { get; }
        [DataMember] public int Level { get; set; }
        [DataMember] public int MaxLife { get; set; }
        [DataMember] public int Damage { get; set; }
        [DataMember] public int Defense { get; set; }
        [DataMember] public double CritChance { get; set; }
        [DataMember] public DamageType DamageType { get; set; }
        [DataMember] public MagicType MagicType { get; set; }

        [DataMember] public Dictionary<string, int> Buffs { get; }
        [DataMember] public Dictionary<DamageType, double> DamageResistance { get; set; }
        [DataMember] public Dictionary<MagicType, double> MagicResistance { get; set; }


        public override string ToString() => Name;


        public Entity()
        {
            Level = 1;
            Buffs = new Dictionary<string, int>(5);
            DamageResistance = new Dictionary<DamageType, double>(4);
            MagicResistance = new Dictionary<MagicType, double>(5);
        }


        public virtual string UpdateBuffs()
        {
            var msg = new StringBuilder();

            foreach (var (buff, duration) in Buffs.Select(x => (x.Key, x.Value)).ToArray())
            {
                msg.AppendLine(buff.Buff().Effects(this));
                if (duration == 1) Buffs.Remove(buff);
                else Buffs[buff] -= 1;
            }

            return msg.ToString();
        }


        public virtual bool Hit(int damage, DamageType type, MagicType magic)
        {
            if (!DamageResistance.TryGetValue(type, out var dmgRes)) dmgRes = 0;
            if (!MagicResistance.TryGetValue(magic, out var magicRes)) magicRes = 0;

            double modified = (damage - Defense) * (1 - dmgRes) * (1 - magicRes);
            Life -= modified.Ceiling();

            return true;
        }


        public virtual string Attack(Entity target)
        {
            bool crit = Bot.Random.NextDouble() < CritChance;
            int previousLife = target.Life;
            target.Hit(Damage * (crit ? 2 : 1), DamageType, MagicType);

            string critMessage = crit ? "Critical hit!" : "";
            return $"{this} dealt {previousLife - target.Life} damage to {target}. {critMessage} ";
        }
    }
}
