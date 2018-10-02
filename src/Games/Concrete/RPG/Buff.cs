
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

        public virtual string TickEffects(Entity holder) => "";
        public virtual void BuffEffects(Entity holder) { }
    }
}
