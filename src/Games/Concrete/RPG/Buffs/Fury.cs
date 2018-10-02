using System;
using System.Collections.Generic;
using System.Text;

namespace PacManBot.Games.Concrete.RPG.Buffs
{
    public class Fury : Buff
    {
        public override string Name => "Fury";
        public override string Icon => "🔺";
        public override string Description => "Greatly increased damage";

        public override void BuffEffects(Entity holder)
        {
            holder.DamageMult += 0.5;
        }
    }
}
