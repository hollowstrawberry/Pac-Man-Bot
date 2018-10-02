using System;
using System.Collections.Generic;
using System.Text;

namespace PacManBot.Games.Concrete.RPG.Buffs
{
    public class Vulnerable : Buff
    {
        public override string Name => "Vulnerable";
        public override string Icon => "☀";
        public override string Description => "Reduces defense by 4";

        public override void BuffEffects(Entity holder)
        {
            holder.Defense -= 4;
        }
    }
}
