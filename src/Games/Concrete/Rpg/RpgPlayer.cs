using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using Discord;
using PacManBot.Constants;
using PacManBot.Extensions;
using PacManBot.Utils;

namespace PacManBot.Games.Concrete.Rpg
{
    /// <summary>
    /// A player fighting entity. Contains most information about the user's savefile.
    /// </summary>
    [DataContract]
    public class RpgPlayer : Entity
    {
        /// <summary>Maximum level a player can reach.</summary>
        public const int LevelCap = 50;
        /// <summary>Maximum value an invested skill can have.</summary>
        public const int SkillMax = 30;


        /// <summary>The player's name.</summary>
        public sealed override string Name => name;

        /// <summary>The experience required to advance to the next level.</summary>
        public int NextLevelExp => Level == 1 ? 4 : 6 * (Level - 1);
        /// <summary>Mana regenerated after each battle.</summary>
        public int ManaRegen => (Level + 10) / 20;
        /// <summary>All equipment currently used by this player.</summary>
        public IEnumerable<Equipment> ActiveEquipment => new[] { armor, weapon }.Select(x => x.GetEquip());
        /// <summary>All skills the player can use.</summary>
        public List<Skill> UnlockedSkills => RpgExtensions.SkillTypes.Values
            .Where(x => x.SkillGet <= spentSkill[x.Type]).ToList().Sorted();
                


        /// <summary>Used for active skills.</summary>
        public virtual int Mana
        {
            get => internalMana;
            set => internalMana = Math.Clamp(value, 0, MaxMana);
        }
        /// <summary>Profile embed color.</summary>
        public Color Color
        {
            set => rawColor = value.RawValue;
            get => new Color(rawColor);
        }


        [DataMember] private string name;
        [DataMember] private int internalMana;
        [DataMember] private uint rawColor;

        /// <summary>The player's current level.</summary>
        [DataMember] public int Level { get; set; } = 1;
        /// <summary>The player's maximum mana.</summary>
        [DataMember] public int MaxMana { get; set; } = 1;
        /// <summary>The current experience in the current level.</summary>
        [DataMember] public int experience;
        /// <summary>The number of unspent skill points.</summary>
        [DataMember] public int skillPoints;
        /// <summary>Skill points spent in each skill line.</summary>
        [DataMember] public Dictionary<SkillType, int> spentSkill;
        /// <summary>The key of the weapon the player is holding.</summary>
        [DataMember] public string weapon = nameof(Weapons.Fists);
        /// <summary>The key of the armor the player is wearing.</summary>
        [DataMember] public string armor = nameof(Armors.Clothes);
        /// <summary>Contains the keys of the items in the player's inventory.</summary>
        [DataMember] public List<string> inventory = new List<string>(20);

 

        private RpgPlayer() { }

        public RpgPlayer(string name) : base()
        {
            SetName(name);

            inventory = new List<string>
            {
                nameof(Weapons.Stick),
            };
            spentSkill = new Dictionary<SkillType, int>(3)
            {
                { SkillType.Dmg, 0 }, { SkillType.Def, 0 }, { SkillType.Crit, 0 },
            };

            UpdateStats();
            Life = MaxLife;
            Mana = MaxMana;
        }


        /// <summary>Updates the player's stats affected by items, skills, buffs, etc.</summary>
        public sealed override void UpdateStats()
        {
            MaxLife = 45 + 5 * Level;
            MaxMana = 1 + Level / 5;
            if (Level == LevelCap)
            {
                MaxLife += 5;
                MaxMana += 1;
            }
            Damage = spentSkill[SkillType.Dmg] / 2;
            Defense = spentSkill[SkillType.Def] / 2;
            CritChance = 0.01 * (spentSkill[SkillType.Crit] / 2);
            DamageMult = 1;
            DefenseMult = 1;
            DamageBoost.Clear();
            MagicBoost.Clear();
            DamageResistance.Clear();
            MagicResistance.Clear();

            foreach (var equip in ActiveEquipment) equip.EquipEffects(this);

            Life = Life; // Clamps if out of bounds
            Mana = Mana;

            base.UpdateStats();
        }


        /// <summary>Safely equips an item from the inventory.</summary>
        public void EquipItem(string key)
        {
            var equip = key.GetEquip();
            if (equip == null) throw new ArgumentException($"{key} is not a valid equipment key.");
            if (!inventory.Contains(key)) throw new InvalidOperationException($"{key} is not in the player's inventory.");

            if (equip is Weapon) weapon = key;
            else if (equip is Armor) armor = key;
            UpdateStats();
        }


        public sealed override string Attack(Entity target)
        {
            string effectMessage = weapon.GetWeapon().AttackEffects(this, target);
            return base.Attack(target) + effectMessage;
        }


        /// <summary>Apply all effects when a player dies and returns a death message.</summary>
        public string Die()
        {
            experience = Math.Max(experience - NextLevelExp / 2, experience / 3);
            Life = MaxLife;
            Mana = MaxMana;
            Buffs.Clear();
            return $"\n☠ You died and lost EXP!\nYou rest and feel ready to battle again.";
        }


        /// <summary> Safely sets the player's name. </summary>
        public void SetName(string text)
        {
            name = text?.Trim().SanitizeMarkdown();
        }


        /// <summary>
        /// Levels up the player if possible, returning a description of the level-up bonuses.
        /// Otherwise returns null.
        /// </summary>
        public string TryLevelUp()
        {
            if (experience < NextLevelExp) return null;

            if (Level >= LevelCap)
            {
                experience = NextLevelExp;
                return null;
            }

            experience -= NextLevelExp;
            Level++;
            UpdateStats();

            var boosts = new List<string>(5);

            Life = MaxLife;
            Mana = MaxMana;
            boosts.Add($"+{(Level == LevelCap ? 10 : 5)} HP");
            if (Level % 5 == 0) boosts.Add($"+{(Level == LevelCap ? 2 : 1)} MP");
            if ((Level + 10) % 20 == 0) boosts.Add("+1 mana regen");

            UpdateStats();

            int sp = Level % 50 == 0 ? 3 : Level % 5 == 0 ? 2 : 1;
            skillPoints += sp;
            boosts.Add($"+{sp} skill point{"s".If(sp > 1)}");
     
            var newEquips = RpgExtensions.EquipTypes
                .Where(pair => pair.Value.LevelGet > 0 && pair.Value.LevelGet <= Level)
                .Select(pair => pair.Key)
                .Where(e => !inventory.Contains(e));

            if (newEquips.Count() > 0)
            {
                inventory.AddRange(newEquips);
                boosts.Add($"new equipment!");
            }

            return boosts.JoinString(", ");
        }


        /// <summary>Obtain a Discord embed with this person's profile.</summary>
        public EmbedBuilder Profile(string channelPrefix = "", bool reaction = false)
        {
            UpdateStats();

            var embed = new EmbedBuilder
            {
                Title = $"{Name}'s Profile",
                Color = Color,
                Description =
                $"\n**You have unspent skill points!**\nᅠ\n".If(skillPoints > 0) +
                (reaction ? "React again for skills" : $"Do **{channelPrefix}rpg skills** for skills."),
            };

            string statsDesc = $"**Level {Level}**  (`{experience}/{NextLevelExp} EXP`)" +
                               $"\nStatus: `{Life}/{MaxLife}`{CustomEmoji.Life}`{Mana}/{MaxMana}`{CustomEmoji.Mana}" +
                               $"\nDefense: `{Defense}`";

            string buffDesc = Buffs.Select(b => $"`{b.timeLeft}`{b.Icon}{b.Name}: {b.Description}").JoinString("\n");

            var wp = weapon.GetWeapon();
            string weaponDesc = $"**[{wp.Name}]**\n*\"{wp.Description}\"*"
                              + $"\n`{Damage}` {wp.Type}{$"/{wp.Magic}".If(wp.Magic != MagicType.Magicless)} damage"
                              + $"\n`{(CritChance * 100).Round()}%` critical hit chance";

            var arm = armor.GetArmor();
            string armorDesc = $"**[{arm.Name}]**\n*\"{arm.Description}\"*\n{arm.EffectsDesc}";

            var invDesc = inventory.Select(x => $"`{x.GetItem().Name}`").JoinString(", ");

            embed.AddField("\\📊Stats", statsDesc, buffDesc.Length > 0);
            if (buffDesc.Length > 0) embed.AddField("Active buffs", buffDesc, true);
            embed.AddField("\\🗡Weapon", weaponDesc, true);
            embed.AddField("\\🛡Armor", armorDesc, true);
            embed.AddField("\\💼Inventory", invDesc == "" ? "*Empty*" : invDesc);

            return embed;
        }


        /// <summary>Obtain a Discord embed displaying this person's skills.</summary>
        public EmbedBuilder Skills(string channelPrefix = "", bool reaction = false)
        {
            var desc = new StringBuilder();

            if (reaction)
            {
                desc.AppendLine($"React again to close\n");
            }
            if (skillPoints > 0)
            {
                desc.AppendLine($"You have {skillPoints} unused skill points!" +
                                $"\nUse the command **{channelPrefix}rpg spend [skill] [amount]** to spend them.\n");
            }

            var unlocked = UnlockedSkills.Select(x =>
                $"**[{x.Name}]** {x.ManaCost}{CustomEmoji.Mana}| Command: `{channelPrefix}rpg {x.Shortcut}`" +
                $"\n*{x.Description}*");

            desc.AppendLine("__**Active Skills:**__");
            desc.AppendLine(unlocked.Count() == 0 ? "*None unlocked*" : $"{unlocked.JoinString("\n")}\nᅠ");


            var embed = new EmbedBuilder
            {
                Title = $"{Name}'s Skills",
                Color = Color,
                Description = desc.ToString().Truncate(2048),
            };

            embed.AddField(SkillField("Power", SkillType.Dmg,
                $"Represents your mastery of weapons.\nEvery 2 power increases damage by 1"));

            embed.AddField(SkillField("Grit", SkillType.Def,
                $"Represents your ability to take hits.\nEvery 2 grit increases defense by 1"));

            embed.AddField(SkillField("Focus", SkillType.Crit,
                $"Represents your capacity to target enemy weak points.\nEvery 2 focus increases critical hit chance by 1%"));

            return embed;
        }


        private EmbedFieldBuilder SkillField(string title, SkillType type, string desc)
        {
            var active = RpgExtensions.SkillTypes.Values.Where(x => x.Type == type).ToList().Sorted();

            return new EmbedFieldBuilder
            {
                Name = $"{type.Icon()}{title} `{spentSkill[type]}/{SkillMax}`",
                IsInline = true,
                Value =
                $"`[{ProgressBar(spentSkill[type], SkillMax, active.Select(x => x.SkillGet))}]`" +
                $"\n{desc}\n__Active skills:__\n" +
                active.Select(x => x.SkillGet <= spentSkill[type] ? x.Name : $"`{x.SkillGet}: {x.Name}`").JoinString("\n")
                + "\nᅠ".If(type < EnumTraits<SkillType>.MaxValue), // padding
            };
        }


        private static string ProgressBar(int current, int max, IEnumerable<int> checkpoints)
        {
            var bar = new char[max];
            for (int i = 0; i < max; i++)
            {
                bar[i] = checkpoints.Contains(i + 1) ? (i < current ? 'O' : 'o') : (i < current ? '|' : '.');
            }
            return bar.JoinString();
        }
    }
}
