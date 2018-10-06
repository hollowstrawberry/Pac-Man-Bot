
namespace PacManBot.Games.Concrete.Rpg
{
    /// <summary>
    /// An item that can be equipped by a player in the RPG.
    /// </summary>
    public abstract class Equipment : Item
    {
        /// <summary>Player level this item will always be obtained at.</summary>
        public virtual int LevelGet => -1;

        /// <summary>Passive effects applied constantly to the player wearing this item.</summary>
        public virtual void EquipEffects(RpgPlayer player)
        {
        }
    }
}
