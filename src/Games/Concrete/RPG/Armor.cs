using System;
using System.Collections.Generic;
using System.Text;

namespace PacManBot.Games.Concrete.RPG
{
    public abstract class Armor : Equipment
    {
        public abstract string EffectsDesc { get; }
    }
}
