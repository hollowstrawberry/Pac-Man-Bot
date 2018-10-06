using System;

namespace PacManBot.Games.Concrete.Rpg
{
    /// <summary>
    /// Represents an active skill the player can use in battle.
    /// </summary>
    public abstract class Skill : IKeyable, IComparable<Skill>
    {
        public virtual string Key => GetType().Name;

        /// <summary>The visible name of this active skill.</summary>
        public abstract string Name { get; }
        /// <summary>The visible description of this active skill.</summary>
        public abstract string Description { get; }
        /// <summary>The unique command this skill can be called by in battle.</summary>
        public abstract string Shortcut { get; }
        /// <summary>The mana used by this skill when used.</summary>
        public abstract int ManaCost { get; }
        /// <summary>The skill line this skill belongs to.</summary>
        public abstract SkillType Type { get; }
        /// <summary>The invested skill points needed in the skill line to unlock this skill.</summary>
        public abstract int SkillGet { get; }

        /// <summary>The effects this skill has on the game when activated by the player.</summary>
        public abstract string Effect(RpgGame game);

        public override string ToString() => Name;
        public override bool Equals(object obj) => obj is Skill other && Key == other.Key;
        public override int GetHashCode() => Key.GetHashCode();


        public int CompareTo(Skill other)
        {
            int val = Type.CompareTo(other.Type);
            if (val == 0) val = SkillGet.CompareTo(other.SkillGet);
            return val;
        }
    }
}
