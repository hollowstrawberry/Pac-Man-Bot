
namespace PacManBot.Games.Concrete.Rpg.Buffs
{
    public class Immune : Buff
    {
        public override string Name => "Immune";
        public override string Icon => "🛡";
        public override string Description => "Immune to damage";

        public override void BuffEffects(Entity holder)
        {
            holder.Defense = 1000;
        }
    }
}
