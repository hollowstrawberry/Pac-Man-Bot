using System;
using System.Runtime.Serialization;

namespace PacManBot.Games.Concrete.RPG
{
    [DataContract]
    public abstract class Enemy : Entity, IKeyable
    {
        public virtual string Key => GetType().Name;

        public Enemy() : base()
        {
            SetDefaults();
            Life = MaxLife;
        }


        public abstract void SetDefaults();


        public Enemy MakeNew()
        {
            return (Enemy)Activator.CreateInstance(GetType());
        }
    }
}
