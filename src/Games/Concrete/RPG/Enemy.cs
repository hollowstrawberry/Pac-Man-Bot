using System;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Discord;
using PacManBot.Extensions;

namespace PacManBot.Games.Concrete.Rpg
{
    /// <summary>
    /// A non-player fighting entity in the RPG.
    /// </summary>
    [DataContract]
    public abstract class Enemy : Entity, IKeyable
    {
        public virtual string Key => GetType().Name;

        public abstract string Description { get; }
        public abstract int Level { get; }
        public abstract int ExpYield { get; }
        public abstract int BaseDamage { get; }
        public abstract int BaseDefense { get; }
        public abstract double BaseCritChance { get; }

        //public override bool Equals(object obj) => obj is Enemy en && Key == en.Key;
        //public override int GetHashCode() => Key.GetHashCode();


        [JsonConstructor]
        private Enemy(string Name) { /* Do nothing on deserialization */ }

        protected Enemy() : base()
        {
            SetStats();
            CalculateStats();
            Life = MaxLife;
        }


        /// <summary>Sets all of this enemy's stats.</summary>
        public abstract void SetStats();


        public sealed override void CalculateStats()
        {
            Damage = BaseDamage;
            Defense = BaseDefense;
            CritChance = BaseCritChance;

            DamageBoost = new Dictionary<DamageType, int>(4);
            MagicBoost = new Dictionary<MagicType, int>(4);
            DamageMult = 1;
            DefenseMult = 1;

            base.CalculateStats();
        }


        /// <summary>Clones an enemy of this type, ready for battle.</summary>
        public Enemy MakeNew()
        {
            return (Enemy)Activator.CreateInstance(GetType(), true);
        }


        /// <summary>Returns a Discord embed field about this enemy.</summary>
        public virtual EmbedFieldBuilder Summary()
        {
            var desc = new StringBuilder();

            desc.AppendLine($"**Level {Level}**");
            desc.AppendLine($"`{$"{Life}/".If(Life < MaxLife)}{MaxLife}` HP, `{Defense}` defense");
            desc.AppendLine($"`{Damage}` {DamageType}{$"/{MagicType}".If(MagicType != MagicType.None)} damage");
            desc.AppendLine($"`{(CritChance*100).Round()}%` critical hit chance");

            if (DamageResistance.Count > 0 || MagicResistance.Count > 0)
            {
                var restList = DamageResistance.Select(x => $"`{(int)(x.Value * 100)}%` {x.Key}")
                    .Concat(MagicResistance.Select(x => $"`{(int)(x.Value * 100)}%` {x.Key}"))
                    .JoinString(", ");

                desc.AppendLine($"Resists {restList}");
            }

            desc.AppendLine($"*\"{Description}\"*");

            return new EmbedFieldBuilder
            {
                Name = $"Summary: {Name}",
                Value = desc.ToString(),
                IsInline = true,
            };
        }
    }
}
