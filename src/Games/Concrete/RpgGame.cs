using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Discord;
using PacManBot.Constants;
using PacManBot.Extensions;
using PacManBot.Games.Concrete.Rpg;

namespace PacManBot.Games.Concrete
{
    [DataContract]
    public class RpgGame : ChannelGame, IUserGame, IStoreableGame, IReactionsGame
    {
        public override string GameName => "ReactionRPG";
        public override int GameIndex => 2;
        public string FilenameKey => "rpg";
        public override TimeSpan Expiry => TimeSpan.FromDays(365);


        public const int NameCharLimit = 32;
        public const string MenuEmote = "🛂";
        public const string ProfileEmote = "🚹";
        public const string SkillsEmote = "🅿";
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
        [DataMember] public override GameState State { get => base.State; set => base.State = value; }
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
                        ResetBattle(GameState.Completed);
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
            State = GameState.Cancelled;
        }


        /// <summary>Prepares a new fight.</summary>
        public void StartFight(ulong? pvpUserId = null)
        {
            State = GameState.Active;
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

            enemies.Add(Program.Random.Choose(possible).MakeNew());

            if (!Program.Random.OneIn(player.Level - enemies[0].Level)) // 2 levels below
            {
                possible = possible.Where(x => x.Level <= player.Level - 2).ToList();
                enemies.Add(Program.Random.Choose(possible).MakeNew());

                if (!Program.Random.OneIn(Math.Max(0, player.Level - enemies[1].Level - 3))) // 5 levels below
                {
                    enemies.Add(Program.Random.Choose(possible).MakeNew());
                }
            }
        }


        /// <summary>Returns an embed containing secondary information about the current fight.</summary>
        public EmbedBuilder FightMenu()
        {
            var embed = new EmbedBuilder
            {
                Title = $"⚔ ReactionRPG Battle",
                Description = "React again to close",
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

                    desc.AppendLine($"{en} was defeated! +{exp} EXP");
                    player.experience += exp;
                    string lvlUp = player.TryLevelUp();
                    if (lvlUp != null)
                    {
                        Channel.SendMessageAsync(options: PmBot.DefaultOptions, text:
                            $"\n⏫ Level up! {lvlUp}" +
                            "\n⭐ **You reached the maximum level! Congratulations!**".If(player.Level == RpgPlayer.LevelCap));
                    }

                    enemies.RemoveAt(i);
                }
            }


            if (Opponents.Count == 0)
            {
                embed.Color = Colors.Green;
                desc.AppendLine($"\n🎺 You win!");
                ResetBattle(GameState.Win);
                player.Mana += player.ManaRegen;
            }
            else if (player.Life == 0)
            {
                embed.Color = Colors.Red;
                desc.AppendLine(player.Die());
                ResetBattle(GameState.Lose);
            }


            embed.Description = desc.ToString();

            return embed;
        }


        /// <summary>Returns an embed displaying the current PVP fight, performing an action first if applicable.</summary>
        public EmbedBuilder FightPvP(bool attack = false, Skill skill = null)
        {
            var players = new[] { this, PvpGame }.OrderBy(g => g.OwnerId).Select(g => g.player);

            var embed = new EmbedBuilder
            {
                Title = players.JoinString(" ⚔ "),
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
                PvpGame.ResetBattle(GameState.Lose);
                ResetBattle(GameState.Win);

            }
            else if (player.Life == 0)
            {
                embed.Color = PvpGame.player.Color;
                embed.ThumbnailUrl = PvpGame.Owner.GetAvatarUrl();
                desc.AppendLine($"\n🎺 {PvpGame.Owner.Mention} won!");
                desc.AppendLine($"You both should heal.");

                player.Life = 1;
                player.Buffs.Clear();
                PvpGame.ResetBattle(GameState.Win);
                ResetBattle(GameState.Lose);
            }
            else
            {
                embed.Color = isPvpTurn ? player.Color : PvpGame.player.Color;
                embed.ThumbnailUrl = isPvpTurn ? Owner.GetAvatarUrl() : PvpGame.Owner.GetAvatarUrl();

                if (PvpBattleConfirmed)
                {
                    desc.AppendLine($"{(isPvpTurn ? this : PvpGame).Owner.Mention}'s turn");
                }
                else
                {
                    string prefix = storage.GetPrefix(Channel);
                    desc.AppendLine($"Waiting for {PvpGame.Owner.Mention} to accept the challenge.");
                }
            }


            embed.Description = desc.ToString();

            return embed;
        }


        /// <summary>Safely ends a battle, and sets the game state to a non-active state.</summary>
        public void ResetBattle(GameState endState)
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
                return PvpBattleConfirmed &&
                    (isPvpTurn && userId == OwnerId || PvpGame.isPvpTurn && userId == pvpUserId);
            }

            string emote = value.Mention();
            if (userId != OwnerId) return false;

            int index = EmoteNumberInputs.IndexOf(emote);
            if (index >= 0) return index < Opponents.Count;
            else return EmoteOtherInputs.Contains(emote);
        }


        public async Task InputAsync(IEmote input, ulong userId = 1)
        {
            var emote = input.Mention();

            if (IsPvp)
            {
                if (userId == pvpUserId) // spaghetti
                {
                    await PvpGame.InputAsync(input, userId);
                    return;
                }
                lastEmote = emote;
                var otherGame = PvpGame;
                fightEmbed = FightPvP(true);
                otherGame.fightEmbed = fightEmbed;
                await games.SaveAsync(otherGame);
            }
            else if (emote == MenuEmote)
            {
                if (lastEmote == emote)
                {
                    lastEmote = null;
                    fightEmbed = Fight();
                }
                else
                {
                    lastEmote = emote;
                    fightEmbed = FightMenu();
                }
            }
            else if (emote == ProfileEmote)
            {
                if (lastEmote == emote)
                {
                    lastEmote = SkillsEmote;
                    fightEmbed = player.Skills(storage.GetPrefix(Channel), true);
                }
                else if (lastEmote == SkillsEmote)
                {
                    lastEmote = null;
                    fightEmbed = Fight();
                }
                else
                {
                    lastEmote = emote;
                    fightEmbed = player.Profile(storage.GetPrefix(Channel), reaction: true);
                }
            }
            else
            {
                int index = EmoteNumberInputs.IndexOf(emote);
                if (index < 0 || index >= Opponents.Count) return;

                lastEmote = emote;
                fightEmbed = Fight(index);
            }

            await games.SaveAsync(this);
        }


        public override EmbedBuilder GetEmbed(bool showHelp = true)
        {
            return fightEmbed ?? new EmbedBuilder { Title = GameName, Description = "Error" };
        }




        public void PostDeserialize(IServiceProvider services)
        {
            if (lastBattle > LastPlayed) LastPlayed = lastBattle; // Oof.
            SetServices(services);
        }
    }
}
