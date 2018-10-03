using System;

namespace PacManBot.Games.Concrete.Rpg
{
    public abstract class Item : IKeyable, IEquatable<Item>, IEquatable<string>
    {
        public virtual string Key => GetType().Name;
        public abstract string Name { get; }
        public abstract string Description { get; }


        public override string ToString() => Name;
        public override bool Equals(object obj) => obj is Item other && Key == other.Key;
        public override int GetHashCode() => Key.GetHashCode();

        public bool Equals(Item other) => Key == other?.Key || Name.ToLower() == other?.Name.ToLower();
        public bool Equals(string str) => Key == str || Name.ToLower() == str.ToLower();
    }
}
