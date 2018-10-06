using PacManBot.Extensions;

namespace PacManBot.Games.Concrete.Rpg.Skills
{
    public class Outburst : Skill
    {
        public override string Name => "Outburst";
        public override string Description => "Attack a random enemy for 200% damage.";
        public override string Shortcut => "burst";
        public override int ManaCost => 2;
        public override SkillType Type => SkillType.Dmg;
        public override int SkillGet => 15;

        public override string Effect(RpgGame game)
        {
            bool crit = Bot.Random.NextDouble() < game.player.CritChance;
            int dmg = Entity.AttackFormula(game.player.Damage * 2, crit);
            if (crit) dmg = (dmg * 2.0 / 3.0).Round(); // Crits too OP

            var target = Bot.Random.Choose(game.Opponents);

            string effectMessage = game.player.weapon.GetWeapon().AttackEffects(game.player, target);
            int dealt = target.Hit(dmg, game.player.DamageType, game.player.MagicType);
            return $"{game.player} hits {target} for {dealt} damage. {"Critical hit!".If(crit)}\n{effectMessage}";
        }
    }
}
