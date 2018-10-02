using PacManBot.Extensions;

namespace PacManBot.Games.Concrete.RPG.Armors
{
    public class BluntArmor : Armor
    {
        public override string Name => "Knight Armor";
        public override string Description => "You feel cooler already.";
        public override string EffectsDesc => "+3 Blunt damage\n+6 Defense\n10% defense boost";

        public override int LevelGet => 15;

        public override void EquipEffects(Player player)
        {
            player.DamageBoost.ChangeOrSet(DamageType.Blunt, x => x + 3);
            player.Defense += 7;
            player.DefenseMult += 0.1;
        }
    }


    public class CuttingArmor : Armor
    {
        public override string Name => "Hero Armor";
        public override string Description => "Let's cut up some generic bad guys.";
        public override string EffectsDesc => "+6 Cutting damage\n+3 Defense\n10% damage boost";

        public override int LevelGet => 15;

        public override void EquipEffects(Player player)
        {
            player.DamageBoost.ChangeOrSet(DamageType.Cutting, x => x + 6);
            player.Defense += 3;
            player.DamageMult += 0.1;
        }
    }


    public class PierceArmor : Armor
    {
        public override string Name => "Ranger Attire";
        public override string Description => "Stealthy like an elephant wearing socks.";
        public override string EffectsDesc => "+4 Pierce damage\n+3 Defense\n+10% crit chance";

        public override int LevelGet => 15;

        public override void EquipEffects(Player player)
        {
            player.DamageBoost.ChangeOrSet(DamageType.Pierce, x => x + 4);
            player.Defense += 3;
            player.CritChance += 0.1;
        }
    }


    public class MagicArmor : Armor
    {
        public override string Name => "Wizard Robe";
        public override string Description => "Shoot whipped cream from your fingertips.";
        public override string EffectsDesc => "+4 Magic damage\n+3 Defense\n+4 Mana";

        public override int LevelGet => 15;

        public override void EquipEffects(Player player)
        {
            player.DamageBoost.ChangeOrSet(DamageType.Magic, x => x + 4);
            player.Defense += 3;
            player.MaxMana += 4;
        }
    }
}
