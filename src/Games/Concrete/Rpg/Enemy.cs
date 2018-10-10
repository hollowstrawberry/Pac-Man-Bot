using System;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Discord;
using PacManBot.Constants;
using PacManBot.Extensions;

namespace PacManBot.Games.Concrete.Rpg
{
    /// <summary>
    /// A non-player fighting entity.
    /// </summary>
    [DataContract]
    public abstract class Enemy : Entity, IKeyable
    {
        public virtual string Key => GetType().Name;

        /// <summary>The visible description of this enemy type.</summary>
        public abstract string Description { get; }
        /// <summary>The minimum player level this enemy type can be encountered at.</summary>
        public abstract int Level { get; }
        /// <summary>The base experience given by this enemy type when defeated.</summary>
        public abstract int ExpYield { get; }
        /// <summary>The base damage of this enemy type.</summary>
        public abstract int BaseDamage { get; }
        /// <summary>The base defense of this enemy type.</summary>
        public abstract int BaseDefense { get; }
        /// <summary>The base critical hit chance of this enemy type.</summary>
        public abstract double BaseCritChance { get; }


        public override string ToString() => Name;
        public override bool Equals(object obj) => obj is Enemy other && Key == other.Key;
        public override int GetHashCode() => Key.GetHashCode();


        [JsonConstructor]
        private Enemy(string Name) { /* Do nothing on deserialization */ }

        protected Enemy() : base()
        {
            SetStats();
            UpdateStats();
            Life = MaxLife;
        }


        /// <summary>Sets all of this enemy's stats.</summary>
        public abstract void SetStats();


        public sealed override void UpdateStats()
        {
            Damage = BaseDamage;
            Defense = BaseDefense;
            CritChance = BaseCritChance;

            DamageBoost = new Dictionary<DamageType, int>(4);
            MagicBoost = new Dictionary<MagicType, int>(4);
            DamageMult = 1;
            DefenseMult = 1;

            base.UpdateStats();
        }


        /// <summary>Clones an enemy of this type, ready for battle.</summary>
        public Enemy MakeNew()
        {
            return (Enemy)Activator.CreateInstance(GetType(), true);
        }


        /// <summary>Returns a Discord embed field about this enemy.</summary>
        public virtual EmbedFieldBuilder Summary()
        {
            UpdateStats();

            var desc = new StringBuilder();

            desc.AppendLine($"**Level {Level}**");
            desc.AppendLine($"`{$"{Life}/".If(Life < MaxLife)}{MaxLife}`{CustomEmoji.Life} `{Defense}` defense");
            desc.AppendLine($"`{Damage}` {DamageType}{$"/{MagicType}".If(MagicType != MagicType.Magicless)} damage");
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
