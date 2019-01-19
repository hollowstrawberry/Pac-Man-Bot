using System.Collections.Generic;
using PacManBot.Extensions;

namespace PacManBot.Games.Concrete.Rpg.Skills
{
    public class WideAssault : Skill
    {
        public override string Name => "Wide Assault";
        public override string Description => "Hits all enemies for 75% damage.";
        public override string Shortcut => "wide";
        public override int ManaCost => 1;
        public override SkillType Type => SkillType.Dmg;
        public override int SkillGet => 5;

        public override string Effect(RpgGame game)
        {
            int dmg = (game.player.Damage * 0.75).Round();

            var hits = new List<string>(3);
            foreach (var enemy in game.Opponents)
            {
                bool crit = Program.Random.NextDouble() < game.player.CritChance;
                int dealt = enemy.Hit(Entity.ModifiedDamage(dmg, crit), game.player.DamageType, game.player.MagicType);
                hits.Add($"{enemy} for {dealt}{"(!)".If(crit)}");
            }

            return $"{game.player} hits {hits.JoinString(", ")}.";
        }
    }
}
