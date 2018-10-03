using System;

namespace PacManBot.Games.Concrete.Rpg
{
    /// <summary>
    /// An effect that an <see cref="Entity"/> holds for a certain amount of time.
    /// </summary>
    public abstract class Buff : IKeyable
    {
        public virtual string Key => GetType().Name;
        public abstract string Name { get; }
        public abstract string Icon { get; }
        public virtual string Description => "";
        public int timeLeft = 0;

        public virtual string TickEffects(Entity holder) => "";
        public virtual void BuffEffects(Entity holder) { }


        public override string ToString() => Name;
        public override bool Equals(object obj) => obj is Buff other && Key == other.Key;
        public override int GetHashCode() => Key.GetHashCode();


        /// <summary>Clones a buff of this type, ready for use.</summary>
        public Buff MakeNew(int duration)
        {
            var buff = (Buff)Activator.CreateInstance(GetType(), true);
            buff.timeLeft = duration;
            return buff;
        }
    }
}
