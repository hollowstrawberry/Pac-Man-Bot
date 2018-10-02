using System;
using System.Linq;
using System.Collections.Generic;
using PacManBot.Extensions;

namespace PacManBot.Games.Concrete.RPG.Skills
{
    public class FuriousResolve : Skill
    {
        public override string Name => "Furious Resolve";
        public override string Description => "+50% damage for 10 turns.";
        public override string Shortcut => "fury";
        public override int ManaCost => 8;
        public override SkillType Type => SkillType.Dmg;
        public override int SkillGet => 20;

        public override string Effect(RpgGame game)
        {
            game.player.AddBuff(nameof(Buffs.Fury), 10);
            return $"{game.player}'s fury makes him a lot stronger!";
        }
    }
}
