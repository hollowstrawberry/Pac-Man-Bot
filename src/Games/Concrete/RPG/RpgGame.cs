using System;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Discord;
using PacManBot.Constants;
using PacManBot.Extensions;

namespace PacManBot.Games.Concrete.Rpg
{
    [DataContract]
    public class RpgGame : ChannelGame, IUserGame, IStoreableGame, IReactionsGame
    {
        public override string GameName => "Generic RPG";
        public override int GameIndex => 6;
        public string FilenameKey => "rpg";
        public override TimeSpan Expiry => TimeSpan.FromDays(100);


        public const string MenuEmote = "🛂";
        public const string ProfileEmote = "🚹";
        public static readonly IReadOnlyList<string> EmoteNumberInputs = CustomEmoji.NumberCircle.Skip(1).Take(3).ToArray();
        public static readonly IReadOnlyList<string> EmoteOtherInputs = new[] { MenuEmote, ProfileEmote };


        private string lastEmote;
        public EmbedBuilder fightEmbed;

        [DataMember] public Player player;
        [DataMember] public List<Enemy> enemies = new List<Enemy>(3);
        [DataMember] public DateTime lastBattle = default;
        [DataMember] public DateTime lastHeal = default;

        /// <summary>The state of the current or last battle.</summary>
        [DataMember] public override State State { get => base.State; set => base.State = value; }
        [DataMember] public override ulong OwnerId { get => base.OwnerId; protected set => base.OwnerId = value; }
        [DataMember] public override DateTime LastPlayed { get => base.LastPlayed; set => base.LastPlayed = value; }
        [DataMember] public override ulong ChannelId { get => base.ChannelId; set => base.ChannelId = value; }
        [DataMember] public override ulong MessageId { get => base.MessageId; set => base.MessageId = value; }


        private RpgGame() { }

        public RpgGame(string name, ulong userId, IServiceProvider services)
            : base(0, new[] { userId }, services)
        {
            player = new Player(name);
            State = State.Cancelled;
        }


        /// <summary>Prepares a new fight.</summary>
        public void StartFight()
        {
            State = State.Active;
            lastBattle = DateTime.Now;
            enemies.Clear();

            var possible = Extensions.EnemyTypes
                .Select(x => x.Value)
                .Where(x => x.Level <= player.Level)
                .OrderByDescending(x => x.Level)
                .Take(10)
                .ToList();

            enemies.Add(Bot.Random.Choose(possible).MakeNew());

            if (!Bot.Random.OneIn(player.Level - enemies[0].Level))
            {
                possible = possible.Where(x => x.Level <= player.Level - 2).ToList();
                enemies.Add(Bot.Random.Choose(possible).MakeNew());

                if (!Bot.Random.OneIn(Math.Max(0, player.Level - enemies[1].Level - 2)))
                {
                    enemies.Add(Bot.Random.Choose(possible).MakeNew());
                }
            }
        }


        /// <summary>Returns an embed containing secondary information about the current fight.</summary>
        public EmbedBuilder FightMenu()
        {
            var embed = new EmbedBuilder
            {
                Title = $"⚔ Generic RPG Battle",
                Color = Colors.DarkBlack,
            };

            foreach (var en in enemies)
            {
                embed.AddField(en.Summary());
            }

            return embed;
        }


        /// <summary>Returns an embed displaying the current fight, performing an action first if applicable.</summary>
        public EmbedBuilder Fight(int? attack = null, Skill skill = null)
        {
            var embed = new EmbedBuilder
            {
                Title = $"⚔ Generic RPG Battle",
                Color = Colors.DarkBlack,
            };

            var desc = new StringBuilder();


            if (attack != null)
            {
                player.CalculateStats();

                if (skill != null)
                {
                    desc.AppendLine($"{player} uses {skill.Type.Icon()}**{skill}**!\n{skill.Effect(this)}");
                }
                else
                {
                    desc.AppendLine(player.Attack(enemies[attack.Value]));
                }

                foreach (var enemy in enemies)
                {
                    if (enemy.Life > 0)
                    {
                        string eBuffs = enemy.TickBuffs();
                        if (eBuffs != "") desc.AppendLine(eBuffs.Trim());
                        enemy.CalculateStats();
                        desc.AppendLine(enemy.Attack(player));
                    }
                }

                if (player.Life > 0)
                {
                    string pBuffs = player.TickBuffs();
                    if (pBuffs != "") desc.AppendLine(pBuffs.Trim());
                    player.CalculateStats();
                }
            }


            embed.AddField(player.Name,
                $"{player.Life}/{player.MaxLife}{CustomEmoji.Life}{player.Mana}/{player.MaxMana}{CustomEmoji.Mana}" +
                player.Buffs.Select(x => x.Key.GetBuff().Icon).JoinString(" "));

            for (int i = 0; i < enemies.Count; /**/)
            {
                var en = enemies[i];

                if (en.Life > 0)
                {
                    embed.AddField($"{CustomEmoji.NumberCircle[i + 1]}" + en.Name,
                        $"{en.Life}/{en.MaxLife}{CustomEmoji.Life}" + 
                        en.Buffs.Select(x => x.Key.GetBuff().Icon).JoinString(" "), true);
                    i++;
                }
                else
                {
                    int exp = en.ExpYield;
                    if (botConfig.debugRpg.Contains(OwnerId)) exp *= 2;

                    desc.AppendLine($"{en} was defeated! +{exp} EXP");
                    player.experience += exp;
                    string lvlUp = player.TryLevelUp();
                    if (lvlUp != null)
                    {
                        desc.AppendLine($"\n⏫ Level up! {lvlUp}");
                        if (player.Level == Player.LevelCap) desc.AppendLine("\n⭐ **Reached the maximum level! Congratulations!**");
                    }

                    enemies.RemoveAt(i);
                }
            }

            
            if (enemies.Count == 0)
            {
                State = State.Win;
                embed.Color = Colors.Green;
                desc.AppendLine($"\n🎺 You win!");
            }
            else if (player.Life == 0)
            {
                State = State.Lose;
                embed.Color = Colors.Red;
                desc.AppendLine($"\n☠ You died and lost EXP!\nYou rest and feel ready to battle again.");
                enemies.Clear();
                player.experience /= 3;
                player.Life = player.MaxLife;
                player.Mana = player.MaxMana;
                player.Buffs.Clear();
            }

            if (State != State.Active)
            {
                ChannelId = 0;
                MessageId = 0;
            }

            embed.Description = desc.ToString();

            return embed;
        }




        public bool IsInput(IEmote value, ulong userId)
        {
            string emote = value.Mention();
            if (userId != OwnerId) return false;
            
            int index = EmoteNumberInputs.IndexOf(emote);
            if (index >= 0) return index < enemies.Count;
            else return EmoteOtherInputs.Contains(emote);
        }


        public void Input(IEmote input, ulong userId = 1)
        {
            var emoji = input.Mention();

            if (emoji == MenuEmote)
            {
                if (lastEmote == emoji)
                {
                    lastEmote = null;
                    fightEmbed = Fight();
                }
                else
                {
                    lastEmote = emoji;
                    fightEmbed = FightMenu();
                }
            }
            else if (emoji == ProfileEmote)
            {
                if (lastEmote == emoji)
                {
                    lastEmote = null;
                    fightEmbed = Fight();
                }
                else
                {
                    lastEmote = emoji;
                    fightEmbed = player.Profile();
                }
            }
            else
            {
                int index = EmoteNumberInputs.IndexOf(emoji);
                if (index < 0 || index >= enemies.Count) return;

                lastEmote = emoji;
                fightEmbed = Fight(index);
            }

            games.Save(this);
        }


        public override EmbedBuilder GetEmbed(bool showHelp = true)
        {
            return fightEmbed ?? new EmbedBuilder { Title = GameName, Description = "..." };
        }




        public void PostDeserialize(IServiceProvider services)
        {
            SetServices(services);
        }
    }
}
