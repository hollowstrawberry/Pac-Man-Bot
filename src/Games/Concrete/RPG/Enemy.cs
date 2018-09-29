using System;
using System.Runtime.Serialization;
using Newtonsoft.Json;

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


        [JsonConstructor]
        private Enemy(string Name) { }

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
    }
}
