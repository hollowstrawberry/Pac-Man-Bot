using System;
using System.Linq;
using System.Collections.Generic;

namespace PacManBot.Games.Concrete.RPG
{
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
