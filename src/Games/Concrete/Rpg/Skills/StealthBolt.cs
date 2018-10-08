
namespace PacManBot.Games.Concrete.Rpg.Skills
{
    public class StealthBolt : Skill
    {
        public override string Name => "Stealth Bolt";
        public override string Description => "Reduce damage and crit ratio of all enemies for 5 turns.";
        public override string Shortcut => "bolt";
        public override int ManaCost => 2;
        public override SkillType Type => SkillType.Crit;
        public override int SkillGet => 10;

        public override string Effect(RpgGame game)
        {
            foreach (var enemy in game.Opponents)
            {
                enemy.AddBuff<Buffs.Blinded>(5);
            }

            return $"{game.player} cripples the enemy!";
        }
    }
}
