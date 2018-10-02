
namespace PacManBot.Games.Concrete.Rpg.Skills
{
    public class StealthBolt : Skill
    {
        public override string Name => "Stealth Bolt";
        public override string Description => "Reduce damage and crit ratio of all enemies for 4 turns.";
        public override string Shortcut => "bolt";
        public override int ManaCost => 2;
        public override SkillType Type => SkillType.Crit;
        public override int SkillGet => 5;

        public override string Effect(RpgGame game)
        {
            foreach (var enemy in game.enemies)
            {
                enemy.AddBuff(nameof(Buffs.Blinded), 4);
            }

            return $"{game.player} cripples the enemy!";
        }
    }
}
