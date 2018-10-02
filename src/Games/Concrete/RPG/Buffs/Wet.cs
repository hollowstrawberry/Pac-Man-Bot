using System;
using System.Collections.Generic;
using System.Text;

namespace PacManBot.Games.Concrete.Rpg.Buffs
{
    public class Wet : Buff
    {
        public override string Name => "Wet";
        public override string Icon => "💦";
        public override string Description => "Your clothes got a little wet. No effects.";
    }
}
