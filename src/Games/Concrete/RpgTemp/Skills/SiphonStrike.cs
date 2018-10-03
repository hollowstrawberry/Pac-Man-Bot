using System;
using System.Linq;
using System.Collections.Generic;
using PacManBot.Constants;
using PacManBot.Extensions;

namespace PacManBot.Games.Concrete.Rpg.Skills
{
    public class SiphonStrike : Skill
    {
        public override string Name => "Siphon Strike";
        public override string Description => "Hit a random enemy and steal 300% HP.";
        public override string Shortcut => "siphon";
        public override int ManaCost => 3;
        public override SkillType Type => SkillType.Def;
        public override int SkillGet => 15;

        public override string Effect(RpgGame game)
        {
            bool crit = Bot.Random.NextDouble() < game.player.CritChance;
            int dmg = Entity.AttackFormula(game.player.Damage, crit);

            var target = Bot.Random.Choose(game.enemies);

            string effectMessage = game.player.weapon.GetWeapon().AttackEffects(game.player, target);
            int dealt = target.Hit(dmg, game.player.DamageType, game.player.MagicType);
            int heal = dealt * 3;
            game.player.Life += heal;
            return $"{this} dealt {dealt} damage to {target}{" (!)".If(crit)} and siphoned {heal} HP!";
        }
    }
}
