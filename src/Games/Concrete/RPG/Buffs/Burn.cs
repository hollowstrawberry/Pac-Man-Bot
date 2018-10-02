using System;
using System.Collections.Generic;
using System.Text;

namespace PacManBot.Games.Concrete.Rpg.Buffs
{
    public class Burn : Buff
    {
        public override string Name => "Burn";
        public override string Icon => "🔥";
        public override string Description => "Deals 1 damage every turn";

        public override string TickEffects(Entity holder)
        {
            holder.Life -= 1;
            return $"{holder} received 1 damage from a burn!";
        }
    }
}
