using System;

namespace PacManBot.Games.Concrete.Rpg
{
    public abstract class Item : IKeyable, IEquatable<Item>, IEquatable<string>
    {
        public virtual string Key => GetType().Name;
        public abstract string Name { get; }
        public abstract string Description { get; }


        public override string ToString() => Name;
        public override int GetHashCode() => Key.GetHashCode();

        public override bool Equals(object obj)
        {
            switch (obj)
            {
                case Item i: return Equals(i);
                case string str: return Equals(str);
                default: return false;
            }
        }

        public bool Equals(Item other) => Key == other?.Key || Name.ToLower() == other?.Name.ToLower();
        public bool Equals(string str) => Key == str || Name.ToLower() == str.ToLower();
    }
}
