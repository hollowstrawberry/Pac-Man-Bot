using System;
using System.Collections.Generic;
using System.Text;

namespace PacManBot.Games.Concrete.RPG.Buffs
{
    public class Blinded : Buff
    {
        public override string Name => "Blinded";
        public override string Icon => "👁";
        public override string Description => "Reduces damage and crit ratio";

        public override void BuffEffects(Entity holder)
        {
            holder.Damage -= 4;
            holder.CritChance -= 0.15;
        }
    }
}
