using System;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Discord;
using PacManBot.Games.Concrete.Rpg;
using PacManBot.Constants;
using PacManBot.Extensions;

namespace PacManBot.Games.Concrete
{
    [DataContract]
    public class RpgGame : ChannelGame, IUserGame, IStoreableGame, IReactionsGame
    {
        public override string GameName => "ReactionRPG";
        public override int GameIndex => 2;
        public string FilenameKey => "rpg";
        public override TimeSpan Expiry => TimeSpan.FromDays(100);


        public const string MenuEmote = "🛂";
        public const string ProfileEmote = "🚹";
        public const string PvpEmote = "💥";
        public static readonly IReadOnlyList<string> EmoteNumberInputs = CustomEmoji.NumberCircle.Skip(1).Take(3).ToArray();
        public static readonly IReadOnlyList<string> EmoteOtherInputs = new[] { MenuEmote, ProfileEmote };


        public string lastEmote;
        public EmbedBuilder fightEmbed;

        /// <summary>All information about this game's player.</summary>
        [DataMember] public RpgPlayer player;
        /// <summary>Enemies in an enemy battle.</summary>
        [DataMember] public List<Enemy> enemies = new List<Enemy>(3);
        /// <summary>The ID of the opposing user in a PVP battle, proposed or confirmed.</summary>
        [DataMember] public ulong pvpUserId;
        /// <summary>Whether it is this player's turn in a PVP battle.</summary>
        [DataMember] public bool isPvpTurn;
        /// <summary>Time when the last battle began.</summary>
        [DataMember] public DateTime lastBattle = default;
        /// <summary>Time when the player last healed.</summary>
        [DataMember] public DateTime lastHeal = default;

        /// <summary>The state of the current or last battle.</summary>
        [DataMember] public override State State { get => base.State; set => base.State = value; }
        [DataMember] public override ulong OwnerId { get => base.OwnerId; protected set => base.OwnerId = value; }
        [DataMember] public override DateTime LastPlayed { get => base.LastPlayed; set => base.LastPlayed = value; }
        [DataMember] public override ulong ChannelId { get => base.ChannelId; set => base.ChannelId = value; }
        [DataMember] public override ulong MessageId { get => base.MessageId; set => base.MessageId = value; }

        /// <summary>Whether this is a PVP battle, proposed or confirmed.</summary>
        public bool IsPvp => PvpGame != null;
        /// <summary>Whether both challenger and challenged are in the same PVP battle.</summary>
        public bool PvpBattleConfirmed => PvpGame.pvpUserId == OwnerId;
        /// <summary>Opponents in the current battle, whether it's against enemies or a player.</summary>
        public IReadOnlyList<Entity> Opponents => IsPvp ? new Entity[] { PvpGame.player } : (IReadOnlyList<Entity>)enemies;

        private RpgGame internalPvpGame;

        /// <summary>The opponent's game object in a PVP battle. Managed by <see cref="pvpUserId"/>.</summary>
        public RpgGame PvpGame
        {
            get
            {
                if (pvpUserId == 0)
                {
                    internalPvpGame = null;
                }
                else if (pvpUserId != internalPvpGame?.OwnerId) // Inconsistency
                {
                    internalPvpGame = games.GetForUser<RpgGame>(pvpUserId);

                    // Reset fight if invalid opponent
                    if (internalPvpGame == null)
                    {
                        ResetBattle(State.Completed);
                        return null;
                    }
                }

                return internalPvpGame;
            }
        }




        private RpgGame() { }

        public RpgGame(string name, ulong userId, IServiceProvider services)
            : base(0, new[] { userId }, services)
        {
            player = new RpgPlayer(name);
            State = State.Cancelled;
        }


        /// <summary>Prepares a new fight.</summary>
        public void StartFight(ulong? pvpUserId = null)
        {
            State = State.Active;
            lastBattle = DateTime.Now;
            enemies.Clear();

            if (pvpUserId.HasValue)
            {
                this.pvpUserId = pvpUserId.Value;
                return;
            }

            var possible = RpgExtensions.EnemyTypes
                .Select(x => x.Value)
                .Where(x => x.Level <= player.Level)
                .OrderByDescending(x => x.Level)
                .Take(10)
                .ToList();

            enemies.Add(Bot.Random.Choose(possible).MakeNew());

            if (!Bot.Random.OneIn(player.Level - enemies[0].Level)) // 2 levels below
            {
                possible = possible.Where(x => x.Level <= player.Level - 2).ToList();
                enemies.Add(Bot.Random.Choose(possible).MakeNew());

                if (!Bot.Random.OneIn(Math.Max(0, player.Level - enemies[1].Level - 3))) // 5 levels below
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
                Title = $"⚔ ReactionRPG Battle",
                Color = Colors.DarkBlack,
            };

            foreach (var en in Opponents.Cast<Enemy>())
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
                Title = $"⚔ ReactionRPG Battle",
                Color = Colors.DarkBlack,
            };


            var desc = new StringBuilder();


            if (attack != null)
            {
                player.UpdateStats();
                foreach (var op in Opponents) op.UpdateStats();

                if (skill != null) desc.AppendLine($"{player} uses {skill.Type.Icon()}**{skill}**!\n{skill.Effect(this)}");
                else desc.AppendLine(player.Attack(enemies[attack.Value]));

                foreach (var enemy in enemies.Where(e => e.Life > 0))
                {
                    string eBuffs = enemy.TickBuffs();
                    if (eBuffs != "") desc.AppendLine(eBuffs.Trim());
                    enemy.UpdateStats();
                    desc.AppendLine(enemy.Attack(player));
                }


                if (player.Life > 0)
                {
                    string pBuffs = player.TickBuffs();
                    if (pBuffs != "") desc.AppendLine(pBuffs.Trim());
                    player.UpdateStats();
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
                        en.Buffs.Select(b => b.Icon).JoinString(" "), true);
                    i++;
                }
                else
                {
                    int exp = en.ExpYield;
                    if (botConfig.testers.Contains(OwnerId)) exp *= 2;

                    desc.AppendLine($"{en} was defeated! +{exp} EXP");
                    player.experience += exp;
                    string lvlUp = player.TryLevelUp();
                    if (lvlUp != null)
                    {
                        Channel.SendMessageAsync(options: Bot.DefaultOptions,
                            text: $"\n⏫ Level up! {lvlUp}"
                            + "\n⭐ **You reached the maximum level! Congratulations!**".If(player.Level == RpgPlayer.LevelCap));
                    }

                    enemies.RemoveAt(i);
                }
            }


            if (Opponents.Count == 0)
            {

                embed.Color = Colors.Green;
                desc.AppendLine($"\n🎺 You win!");
                ResetBattle(State.Win);
                player.Mana += player.ManaRegen;
            }
            else if (player.Life == 0)
            {
                embed.Color = Colors.Red;
                desc.AppendLine(player.Die());
                ResetBattle(State.Lose);
            }


            embed.Description = desc.ToString();

            return embed;
        }


        /// <summary>Returns an embed displaying the current PVP fight, performing an action first if applicable.</summary>
        public EmbedBuilder FightPvP(bool attack = false, Skill skill = null)
        {
            var embed = new EmbedBuilder
            {
                Title = $"⚔ ReactionRPG Battle",
            };


            var desc = new StringBuilder();


            if (attack)
            {
                isPvpTurn = false;
                PvpGame.isPvpTurn = true;

                player.UpdateStats();
                foreach (var op in Opponents) op.UpdateStats();

                if (skill != null) desc.AppendLine($"{player} uses {skill.Type.Icon()}**{skill}**!\n{skill.Effect(this)}");
                else desc.AppendLine(player.Attack(PvpGame.player));

                if (PvpGame.player.Life > 0)
                {
                    string pBuffs = PvpGame.player.TickBuffs();
                    if (pBuffs != "") desc.AppendLine(pBuffs.Trim());
                }
            }

            var players = new[] { this, PvpGame }.OrderBy(g => g.OwnerId).Select(g => g.player);
            foreach (var p in players)
            {
                embed.AddField(p.Name,
                    $"{p.Life}/{p.MaxLife}{CustomEmoji.Life}{p.Mana}/{p.MaxMana}{CustomEmoji.Mana}" +
                    p.Buffs.Select(x => x.Key.GetBuff().Icon).JoinString(" "));

            }


            if (PvpGame.player.Life == 0)
            {
                embed.Color = player.Color;
                embed.ThumbnailUrl = Owner.GetAvatarUrl();
                desc.AppendLine($"\n🎺 {Owner.Mention} won!");
                desc.AppendLine($"You both should heal.");

                PvpGame.player.Life = 1;
                PvpGame.player.Buffs.Clear();
                PvpGame.ResetBattle(State.Lose);
                games.Save(PvpGame);
                ResetBattle(State.Win);

            }
            else if (player.Life == 0)
            {
                embed.Color = PvpGame.player.Color;
                embed.ThumbnailUrl = PvpGame.Owner.GetAvatarUrl();
                desc.AppendLine($"\n🎺 {PvpGame.Owner.Mention} won!");
                desc.AppendLine($"You both should heal.");

                player.Life = 1;
                player.Buffs.Clear();
                PvpGame.ResetBattle(State.Win);
                games.Save(PvpGame);
                ResetBattle(State.Lose);
            }
            else
            {
                games.Save(PvpGame);

                embed.Color = isPvpTurn ? player.Color : PvpGame.player.Color;
                embed.ThumbnailUrl = isPvpTurn ? Owner.GetAvatarUrl() : PvpGame.Owner.GetAvatarUrl();

                if (PvpBattleConfirmed)
                {
                    desc.AppendLine($"{(isPvpTurn ? this : PvpGame).Owner.Mention}'s turn");
                }
                else
                {
                    string prefix = storage.GetPrefix(Channel);
                    desc.AppendLine($"{PvpGame.player} must challenge you back to start." +
                                    $"\nThe challenger can cancel with {prefix}rpg cancel");
                }
            }


            embed.Description = desc.ToString();

            return embed;
        }


        /// <summary>Safely ends a battle, and sets the game state to a non-active state.</summary>
        public void ResetBattle(State endState)
        {
            State = endState;
            fightEmbed = null;
            pvpUserId = 0;
            enemies.Clear();
            ChannelId = 0;
            MessageId = 0;
        }




        public bool IsInput(IEmote value, ulong userId)
        {
            if (IsPvp)
            {
                return PvpBattleConfirmed && isPvpTurn && userId == OwnerId;
            }

            string emote = value.Mention();
            if (userId != OwnerId) return false;

            int index = EmoteNumberInputs.IndexOf(emote);
            if (index >= 0) return index < Opponents.Count;
            else return EmoteOtherInputs.Contains(emote);
        }


        public void Input(IEmote input, ulong userId = 1)
        {
            var emoji = input.Mention();

            if (IsPvp)
            {
                lastEmote = emoji;
                fightEmbed = FightPvP(true);
                if (IsPvp) PvpGame.fightEmbed = fightEmbed; // Match didn't end
            }
            else if (emoji == MenuEmote)
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
                if (index < 0 || index >= Opponents.Count) return;

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
