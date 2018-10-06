
namespace PacManBot.Games.Concrete.Rpg
{
    /// <summary>
    /// An item that can be equipped in the armor slot.
    /// </summary>
    public abstract class Armor : Equipment
    {
        /// <summary>Visible description of all of this armor's effects.</summary>
        public abstract string EffectsDesc { get; }
    }
}
