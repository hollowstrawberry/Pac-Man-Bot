using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;

namespace PacManBot.Games.Concrete.RPG
{
    /// <summary>
    /// Represents an active skill the player can use in battle.
    /// </summary>
    public abstract class Skill : IKeyable, IComparable<Skill>
    {
        public virtual string Key => GetType().Name;
        public abstract string Name { get; }
        public abstract string Description { get; }
        public abstract string Shortcut { get; }
        public abstract int ManaCost { get; }
        public abstract SkillType Type { get; }
        public abstract int SkillGet { get; }

        public abstract string Effect(RpgGame game);

        public override string ToString() => Name;


        public int CompareTo(Skill other)
        {
            int val = Type.CompareTo(other.Type);
            if (val == 0) val = SkillGet.CompareTo(other.SkillGet);
            return val;
        }
    }
}
