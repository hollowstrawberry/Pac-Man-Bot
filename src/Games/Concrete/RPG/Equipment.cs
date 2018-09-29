using System;
using System.Linq;
using System.Collections.Generic;

namespace PacManBot.Games.Concrete.RPG
{
    /// <summary>
    /// An item that can be equipped by a player in the RPG.
    /// </summary>
    public abstract class Equipment : Item
    {
        public virtual void EquipEffects(Player player)
        {
        }

        public virtual void UnequipEffects(Player player)
        {
        }
    }
}
