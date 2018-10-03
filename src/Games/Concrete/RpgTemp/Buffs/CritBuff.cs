
namespace PacManBot.Games.Concrete.Rpg.Buffs
{
    public class CritBuff : Buff
    {
        public override string Name => "Eagle Eye";
        public override string Icon => "☄";
        public override string Description => "Easy critical hits.";

        public override void BuffEffects(Entity holder)
        {
            holder.CritChance += 0.5;
        }
    }
}
