using PacManBot.Extensions;

namespace PacManBot.Games.Concrete.Rpg.Armors
{
    public class BluntArmor : Armor
    {
        public override string Name => "Knight Armor";
        public override string Description => "You feel cooler already.";
        public override string EffectsDesc => "+2 Blunt damage\n+3 Defense\n10% defense boost";

        public override int LevelGet => 15;

        public override void EquipEffects(Player player)
        {
            player.DamageBoost.ChangeOrSet(DamageType.Blunt, x => x + 2);
            player.Defense += 3;
            player.DefenseMult += 0.1;
        }
    }


    public class CuttingArmor : Armor
    {
        public override string Name => "Hero Armor";
        public override string Description => "Let's cut up some generic bad guys.";
        public override string EffectsDesc => "+3 Cutting damage\n+2 Defense\n10% damage boost";

        public override int LevelGet => 15;

        public override void EquipEffects(Player player)
        {
            player.DamageBoost.ChangeOrSet(DamageType.Cutting, x => x + 3);
            player.Defense += 2;
            player.DamageMult += 0.1;
        }
    }


    public class PierceArmor : Armor
    {
        public override string Name => "Ranger Attire";
        public override string Description => "Stealthy like an elephant wearing socks.";
        public override string EffectsDesc => "+3 Pierce damage\n+2 Defense\n+10% crit chance";

        public override int LevelGet => 15;

        public override void EquipEffects(Player player)
        {
            player.DamageBoost.ChangeOrSet(DamageType.Pierce, x => x + 3);
            player.Defense += 2;
            player.CritChance += 0.1;
        }
    }


    public class MagicArmor : Armor
    {
        public override string Name => "Wizard Robe";
        public override string Description => "Shoot whipped cream from your fingertips.";
        public override string EffectsDesc => "+3 Magic damage\n+2 Defense\n+2 MP";

        public override int LevelGet => 15;

        public override void EquipEffects(Player player)
        {
            player.DamageBoost.ChangeOrSet(DamageType.Magic, x => x + 3);
            player.Defense += 2;
            player.MaxMana += 2;
        }
    }
}
