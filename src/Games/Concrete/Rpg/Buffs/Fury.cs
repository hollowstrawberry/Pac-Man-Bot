
namespace PacManBot.Games.Concrete.Rpg.Buffs
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
