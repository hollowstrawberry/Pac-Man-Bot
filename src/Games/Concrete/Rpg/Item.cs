using System;

namespace PacManBot.Games.Concrete.Rpg
{
    /// <summary>
    /// An item that can be stored in a player's inventory.
    /// </summary>
    public abstract class Item : IKeyable, IEquatable<Item>, IEquatable<string>
    {
        public virtual string Key => GetType().Name;

        /// <summary>Visible name of this item.</summary>
        public abstract string Name { get; }
        /// <summary>Visible description of this item.</summary>
        public abstract string Description { get; }


        public override string ToString() => Name;
        public override bool Equals(object obj) => obj is Item other && Key == other.Key;
        public override int GetHashCode() => Key.GetHashCode();

        public bool Equals(Item other) => Key == other?.Key || Name.Equals(other?.Name, StringComparison.OrdinalIgnoreCase);
        public bool Equals(string str) => Key == str || Name.Equals(str, StringComparison.OrdinalIgnoreCase);
    }
}
