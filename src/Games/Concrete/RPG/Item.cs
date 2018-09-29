
namespace PacManBot.Games.Concrete.RPG
{
    public abstract class Item : IKeyable
    {
        public virtual string Key => GetType().Name;
        public abstract string Name { get; }
        public virtual string Description => "";
    }
}
