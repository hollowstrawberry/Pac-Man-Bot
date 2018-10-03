
namespace PacManBot.Games.Concrete.Rpg.Buffs
{
    public class Blocking : Buff
    {
        public override string Name => "Blocking";
        public override string Icon => "🛡";
        public override string Description => "Greatly increased defense";

        public override void BuffEffects(Entity holder)
        {
            holder.DefenseMult += 0.5;
        }
    }
}
