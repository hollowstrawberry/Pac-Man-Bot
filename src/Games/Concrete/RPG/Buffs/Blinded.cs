using System;
using System.Collections.Generic;
using System.Text;

namespace PacManBot.Games.Concrete.RPG.Buffs
{
    public class Blinded : Buff
    {
        public override string Name => "Blinded";
        public override string Icon => "☀";
        public override string Description => "Reduces enemy defense by 4";

        public override void StartEffects(Entity holder)
        {
            holder.Defense -= 4;
        }

        public override void EndEffects(Entity holder)
        {
            holder.Defense += 4;
        }
    }
}
