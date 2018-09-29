using System;
using System.Collections.Generic;
using System.Text;

namespace PacManBot.Games.Concrete.RPG
{
    public abstract class Buff : IKeyable
    {
        public virtual string Key => GetType().Name;
        public abstract string Name { get; }
        public abstract string Icon { get; }
        public virtual string Description => "";

        public virtual string Effects(Entity holder) => "";
    }
}
