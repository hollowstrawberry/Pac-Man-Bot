
namespace PacManBot.Games.Concrete.Rpg
{
    /// <summary>
    /// The type of damage inflicted by an entity on another.
    /// </summary>
    public enum DamageType
    {
        Blunt,
        Pierce,
        Cutting,
        Magic,
    }


    /// <summary>
    /// The type of damage inflicted by an enitity on another.
    /// </summary>
    public enum MagicType
    {
        Magicless,
        Fire,
        Air,
        Water,
        Earth,

        None = 0, // Backwards compatibility
    }


    /// <summary>
    /// One of a player's skills.
    /// </summary>
    public enum SkillType
    {
        Dmg,
        Def,
        Crit,
    }
}
