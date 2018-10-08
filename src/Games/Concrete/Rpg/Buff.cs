using System;
using System.Runtime.Serialization;

namespace PacManBot.Games.Concrete.Rpg
{
    /// <summary>
    /// An effect that an <see cref="Entity"/> holds for a certain amount of time.
    /// </summary>
    [DataContract]
    public abstract class Buff : IKeyable
    {
        public virtual string Key => GetType().Name;

        /// <summary>The visible name of this buff type.</summary>
        public abstract string Name { get; }
        /// <summary>The emoji used to represent this buff type.</summary>
        public abstract string Icon { get; }
        /// <summary>The visible description of this buff type.</summary>
        public virtual string Description => "";

        /// <summary>How many more turns this buff will tick for before being removed.</summary>
        [DataMember] public int timeLeft = 0;


        /// <summary>Passive effects applied constantly to the entity holding this buff.</summary>
        public virtual void BuffEffects(Entity holder) { }
        /// <summary>Active effects applied each turn to the entity holding this buff.</summary>
        public virtual string TickEffects(Entity holder) => "";

        public override string ToString() => Name;
        public override bool Equals(object obj) => obj is Buff other && Key == other.Key;
        public override int GetHashCode() => Key.GetHashCode();


        /// <summary>Clones a buff of this type, ready for use.</summary>
        public Buff MakeNew(int duration)
        {
            var buff = (Buff)Activator.CreateInstance(GetType());
            buff.timeLeft = duration;
            return buff;
        }
    }
}
