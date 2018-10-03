
namespace PacManBot.Games.Concrete.Rpg.Skills
{
    public class FocusAim : Skill
    {
        public override string Name => "Aim for the Head";
        public override string Description => "+50% crit ratio for the 3 next turns.";
        public override string Shortcut => "aim";
        public override int ManaCost => 5;
        public override SkillType Type => SkillType.Crit;
        public override int SkillGet => 20;

        public override string Effect(RpgGame game)
        {
            game.player.AddBuff(nameof(Buffs.CritBuff), 4);
            return $"{game.player} is in deep focus and will crit easily!";
        }
    }
}
