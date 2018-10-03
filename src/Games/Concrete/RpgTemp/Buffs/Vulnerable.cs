
namespace PacManBot.Games.Concrete.Rpg.Buffs
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
