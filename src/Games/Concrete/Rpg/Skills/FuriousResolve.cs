
namespace PacManBot.Games.Concrete.Rpg.Skills
{
    public class FuriousResolve : Skill
    {
        public override string Name => "Furious Resolve";
        public override string Description => "+50% damage for the next 6 turns.";
        public override string Shortcut => "fury";
        public override int ManaCost => 5;
        public override SkillType Type => SkillType.Dmg;
        public override int SkillGet => 25;

        public override string Effect(RpgGame game)
        {
            game.player.AddBuff<Buffs.Fury>(7);
            return $"{game.player}'s fury makes him a lot stronger!";
        }
    }
}
