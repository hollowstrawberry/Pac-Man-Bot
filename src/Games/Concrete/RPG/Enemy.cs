using System;
using System.Text;
using System.Linq;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Discord;
using PacManBot.Extensions;

namespace PacManBot.Games.Concrete.RPG
{
    /// <summary>
    /// A non-player fighting entity in the RPG.
    /// </summary>
    [DataContract]
    public abstract class Enemy : Entity, IKeyable
    {
        public virtual string Key => GetType().Name;
        public int ExpYield { get; set; } = 1;

        //public override bool Equals(object obj) => obj is Enemy en && Key == en.Key;
        //public override int GetHashCode() => Key.GetHashCode();


        [JsonConstructor]
        private Enemy(string Name) { /* Do nothing on deserialization */ }

        protected Enemy() : base()
        {
            SetStats();
            Life = MaxLife;
        }


        /// <summary>Sets all of this enemy's stats.</summary>
        public abstract void SetStats();


        /// <summary>Clones an enemy of this type, ready for battle.</summary>
        public Enemy MakeNew()
        {
            return (Enemy)Activator.CreateInstance(GetType(), true);
        }


        /// <summary>Returns a Discord embed field about this enemy.</summary>
        public EmbedFieldBuilder Summary()
        {
            var desc = new StringBuilder();

            desc.AppendLine($"**Level {Level}**");
            desc.AppendLine($"`{MaxLife}` MaxHP, `{Defense}` defense");
            desc.AppendLine($"`{Damage}` {DamageType} damage");
            if (MagicType != MagicType.None) desc.Append($"{MagicType} Magic");
            desc.AppendLine($"`{(int)(CritChance*100)}%` critical hit chance");

            if (DamageResistance.Count > 0 || DamageResistance.Count > 0)
            {
                var restList = DamageResistance.Select(x => $"`{(int)(x.Value * 100)}%` {x.Key}")
                    .Concat(MagicResistance.Select(x => $"`{(int)(x.Value * 100)}%` {x.Key}"))
                    .JoinString(", ");

                desc.AppendLine($"**Resistances:** {restList}");
            }

            return new EmbedFieldBuilder
            {
                Name = $"Summary: {Name}",
                Value = desc.ToString(),
                IsInline = true,
            };
        }
    }
}
