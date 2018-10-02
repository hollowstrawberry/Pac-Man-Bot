using System;
using System.Collections.Generic;
using System.Text;

namespace PacManBot.Games.Concrete.Rpg
{
    public abstract class Armor : Equipment
    {
        public abstract string EffectsDesc { get; }
    }
}
