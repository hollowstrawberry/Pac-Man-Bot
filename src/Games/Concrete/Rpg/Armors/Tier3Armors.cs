﻿using PacManBot.Extensions;

namespace PacManBot.Games.Concrete.Rpg.Armors
{
    public class SpectreArmor : Armor
    {
        public override string Name => "Spectre Sheet";
        public override string Description => "OooooOOOOOooo!";
        public override string EffectsDesc => "+15% damage\n+4 MP\n+3 defense\n+20% magic resistance";

        public override int LevelGet => 35;

        public override void EquipEffects(Player player)
        {
            player.DamageMult += 0.15;
            player.MaxMana += 4;
            player.Defense += 3;
            player.DamageResistance.ChangeOrSet(DamageType.Magic, x => x + 0.2);
        }
    }


    public class NinjaSuit : Armor
    {
        public override string Name => "Ninja Suit";
        public override string Description => "Sadly, this game has no dodge stat.";
        public override string EffectsDesc => "+10% damage\n+10% crit chance\n+4 defense\n+20% cutting resistance";

        public override int LevelGet => 40;

        public override void EquipEffects(Player player)
        {
            player.DamageMult += 0.15;
            player.CritChance += 0.1;
            player.Defense += 4;
            player.DamageResistance.ChangeOrSet(DamageType.Cutting, x => x + 0.2);
        }
    }


    public class TitanArmor : Armor
    {
        public override string Name => "Titan Armor";
        public override string Description => "You can barely move, but dayum.";
        public override string EffectsDesc => "+10% damage\n+20 HP\n+6 Defense\n+20% non-magic resistance";

        public override int LevelGet => 45;

        public override void EquipEffects(Player player)
        {
            player.DamageMult += 0.1;
            player.MaxLife += 20;
            player.Defense += 6;
            player.DamageResistance.ChangeOrSet(DamageType.Blunt, x => x + 0.2);
            player.DamageResistance.ChangeOrSet(DamageType.Cutting, x => x + 0.2);
            player.DamageResistance.ChangeOrSet(DamageType.Pierce, x => x + 0.2);
        }
    }
}
