using System;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.Exceptions;
using PacManBot.Constants;
using PacManBot.Extensions;
using PacManBot.Games;
using PacManBot.Games.Concrete;
using PacManBot.Games.Concrete.Rpg;

namespace PacManBot.Commands.Modules
{
    [Category(Categories.Games)]
    [Group("rpg")]
    [Description(
    "Play ReactionRPG, a new game where you beat monsters and level up." +
    "\nThe game is yours. You can play in **any channel** anywhere you go, even DMs with the bot.")]
    [RequireBotPermissions(BaseBotPermissions)]
    public class RpgModule : BaseGameModule<RpgGame>
    {
        [GroupCommand, Aliases("fight", "battle"), Priority(-1)]
        [Description("Battle monsters to earn experience")]
        public async Task RpgMaster(CommandContext ctx, string skillName = "")
        {
            if (Game(ctx) is null)
            {
                await ctx.ReplyAsync($"You haven't started your adventure! Use `{Storage.GetPrefix(ctx)}rpg start` to create a character.");
                return;
            }

            if (string.IsNullOrWhiteSpace(skillName))
            {
                if (Game(ctx).State == GameState.Active)
                {
                    await DeleteGameMessageAsync(ctx);
                }
                else
                {
                    var timeLeft = TimeSpan.FromSeconds(30) - (DateTime.Now - Game(ctx).lastBattle);
                    if (timeLeft > TimeSpan.Zero)
                    {
                        await ctx.ReplyAsync($"{CustomEmoji.Cross} You may battle again in {timeLeft.Humanized(empty: "1 second")}");
                        return;
                    }

                    Game(ctx).StartFight();
                    Game(ctx).fightEmbed = Game(ctx).Fight();
                }

                Game(ctx).fightEmbed = Game(ctx).fightEmbed ?? (Game(ctx).IsPvp ? await Game(ctx).FightPvPAsync() : Game(ctx).Fight());
                var msg = await RespondGameAsync(ctx);

                if (Game(ctx).IsPvp && Game(ctx).PvpBattleConfirmed)
                {
                    Game(ctx).PvpGame.ChannelId = ctx.Channel.Id;
                    Game(ctx).PvpGame.MessageId = msg.Id;
                }

                await SaveGameAsync(ctx);

                await AddBattleEmotes(msg, Game(ctx));
            }
            else
            {
                var skill = Game(ctx) is null ? null
                    : RpgExtensions.SkillTypes.Values.FirstOrDefault(x => x.Shortcut == skillName);

                if (skill is null)
                {
                    await ctx.ReplyAsync($"Unknown RPG command! Do `{ctx.Prefix}rpg manual` for game instructions," +
                        $" or `{ctx.Prefix}help rpg` for a list of commands.");
                    return;
                }

                string response = await UseActiveSkill(ctx, skill);
                if (response is not null) await ctx.ReplyAsync(response);
            }
        }


        public async Task<string> UseActiveSkill(CommandContext ctx, Skill skill)
        {
            if (Game(ctx).State != GameState.Active)
                return "You can only use an active skill during battle!";
            if (Game(ctx).IsPvp && !Game(ctx).isPvpTurn)
                return "It's not your turn.";

            if (!Game(ctx).player.UnlockedSkills.Contains(skill))
                return $"You haven't unlocked the `{skill.Shortcut}` active skill.";
            if (Game(ctx).player.Mana == 0)
                return $"You don't have any {CustomEmoji.Mana}left! You should heal.";
            if (skill.ManaCost > Game(ctx).player.Mana)
                return $"{skill.Name} requires {skill.ManaCost}{CustomEmoji.Mana}" +
                       $"but you only have {Game(ctx).player.Mana}{CustomEmoji.Mana}";

            Game(ctx).player.UpdateStats();
            foreach (var op in Game(ctx).Opponents) op.UpdateStats();

            var msg = await Game(ctx).GetMessageAsync();
            Game(ctx).player.Mana -= skill.ManaCost;
            if (Game(ctx).IsPvp)
            {
                var otherGame = Game(ctx).PvpGame;
                Game(ctx).fightEmbed = await Game(ctx).FightPvPAsync(true, skill);
                if (Game(ctx).IsPvp) // Match didn't end
                {
                    Game(ctx).isPvpTurn = false;
                    otherGame.isPvpTurn = true;
                    otherGame.fightEmbed = Game(ctx).fightEmbed;
                }
                await Games.SaveAsync(otherGame);
            }
            else
            {
                Game(ctx).fightEmbed = Game(ctx).Fight(-1, skill);
            }

            if (Game(ctx).State == GameState.Active)
            {
                if (msg is null || msg.Channel.Id != ctx.Channel.Id)
                {
                    msg = await RespondGameAsync(ctx);
                    await SaveGameAsync(ctx);
                    await AddBattleEmotes(msg, Game(ctx));
                }
                else
                {
                    await SaveGameAsync(ctx);
                    await UpdateGameMessageAsync(ctx, msg);
                }
            }
            else
            {
                await SaveGameAsync(ctx);
                try { await msg.ModifyAsync(default, Game(ctx).fightEmbed?.Build()); }
                catch (NotFoundException) { }
            }

            if (ctx.BotCan(Permissions.ManageMessages)) await ctx.Message.DeleteAsync();

            return null;
        }


        [Command("profile"), Aliases("p", "stats", "inventory", "inv")]
        [Description("See yours or another person's profile")]
        public async Task SendProfile(CommandContext ctx, DiscordMember otherMember = null)
        {
            var rpg = Game(ctx);
            if (otherMember is not null) rpg = Games.GetForUser<RpgGame>(otherMember.Id);

            if (rpg is null)
            {
                if (otherMember is null) await ctx.ReplyAsync($"You can use `{ctx.Prefix}rpg start` to start your adventure."); 
                else await ctx.ReplyAsync("This person hasn't started their adventure.");
                return;
            }

            await ctx.RespondAsync(rpg.player.Profile(ctx.Prefix, own: otherMember is null));
        }


        [Command("skills"), Aliases("skill", "s", "spells")]
        [Description("View your character's skills")]
        public async Task SendSkills(CommandContext ctx)
        {
            if (Game(ctx) is null)
            {
                await ctx.ReplyAsync($"You haven't started your adventure! Use `{Storage.GetPrefix(ctx)}rpg start` to create a character.");
                return;
            }

            await ctx.RespondAsync(Game(ctx).player.Skills(ctx.Prefix));
        }



        [Command("heal"), Aliases("h", "potion")]
        [Description("Heal your character every 5 minutes, and once per battle")]
        public async Task HealPlayer(CommandContext ctx)
        {
            if (Game(ctx) is null)
            {
                await ctx.ReplyAsync($"You haven't started your adventure! Use `{Storage.GetPrefix(ctx)}rpg start` to create a character.");
                return;
            }

            if (Game(ctx).lastHeal > Game(ctx).lastBattle && Game(ctx).State == GameState.Active)
            {
                await ctx.ReplyAsync($"{CustomEmoji.Cross} You already healed during this battle.");
                return;
            }
            else if (Game(ctx).IsPvp && Game(ctx).PvpBattleConfirmed)
            {
                await ctx.ReplyAsync($"{CustomEmoji.Cross} You can't heal in a PVP battle.");
                return;
            }

            var timeLeft = TimeSpan.FromMinutes(5) - (DateTime.Now - Game(ctx).lastHeal);

            if (timeLeft > TimeSpan.Zero)
            {
                await ctx.ReplyAsync($"{CustomEmoji.Cross} You may heal again in {timeLeft.Humanized(empty: "1 second")}");
                return;
            }

            Game(ctx).lastHeal = DateTime.Now;
            Game(ctx).player.Life = Game(ctx).player.MaxLife;
            Game(ctx).player.Mana = Game(ctx).player.MaxMana;
            Game(ctx).player.Buffs.Clear();
            await SaveGameAsync(ctx);

            await ctx.ReplyAsync($"💟 Fully restored!");

            if (Game(ctx).State == GameState.Active)
            {
                var message = await Game(ctx).GetMessageAsync();
                if (message is not null)
                {
                    Game(ctx).lastEmote = "";
                    Game(ctx).fightEmbed = Game(ctx).IsPvp ? await Game(ctx).FightPvPAsync() : Game(ctx).Fight();
                    await UpdateGameMessageAsync(ctx);
                }

                if (ctx.BotCan(Permissions.ManageMessages))
                {
                    await ctx.Message.DeleteAsync();
                }
            }
        }


        [Command("equip"), Aliases("e", "weapon", "armor")]
        [Description("Equip an item from your inventory")]
        public async Task EquipItem(CommandContext ctx, [RemainingText] string itemName)
        {
            if (Game(ctx) is null)
            {
                await ctx.ReplyAsync($"You haven't started your adventure! Use `{Storage.GetPrefix(ctx)}rpg start` to create a character.");
                return;
            }

            if (string.IsNullOrWhiteSpace(itemName))
            {
                await ctx.ReplyAsync("You must specify an item from your inventory.");
                return;
            }

            Equipment bestMatch = null;
            double bestPercent = 0;
            foreach (var item in Game(ctx).player.inventory.Select(x => x.GetEquip()))
            {
                double sim = itemName.Similarity(item.Name, false);
                if (sim > bestPercent)
                {
                    bestMatch = item;
                    bestPercent = sim;
                }
                if (sim == 1) break;
            }

            if (bestPercent < 0.69)
            {
                await ctx.ReplyAsync($"Can't find a weapon with that name in your inventory." +
                    $" Did you mean `{bestMatch}`?".If(bestPercent > 0.39));
                return;
            }
            if (bestMatch is Armor && Game(ctx).State == GameState.Active)
            {
                await ctx.ReplyAsync("You can't switch armors mid-battle (but you can switch weapons).");
            }

            Game(ctx).player.EquipItem(bestMatch.Key);
            await SaveGameAsync(ctx);
            await ctx.ReplyAsync($"⚔ Equipped `{bestMatch}`.");

            if (Game(ctx).State == GameState.Active && !Game(ctx).IsPvp)
            {
                Game(ctx).lastEmote = RpgGame.ProfileEmote;
                Game(ctx).fightEmbed = Game(ctx).player.Profile(ctx.Prefix, reaction: true);
                await UpdateGameMessageAsync(ctx);

                if (ctx.BotCan(Permissions.ManageMessages))
                {
                    await ctx.Message.DeleteAsync();
                }
            }
        }


        [Command("spend")]
        [Description("Use your skill points in power, grit or focus")]
        public async Task SpendSkillPoints(CommandContext ctx, string skill = "", int amount = 1)
        {
            if (Game(ctx) is null)
            {
                await ctx.ReplyAsync($"You haven't started your adventure! Use `{Storage.GetPrefix(ctx)}rpg start` to create a character.");
                return;
            }

            if (string.IsNullOrWhiteSpace(skill))
            {
                await ctx.ReplyAsync("Please specify a skill and amount to spend.");
                return;
            }
            if (amount < 1)
            {
                await ctx.ReplyAsync("Please specify a valid amount of skill points to spend.");
                return;
            }
            if (amount > Game(ctx).player.skillPoints)
            {
                await ctx.ReplyAsync("You don't have that many skill points!");
                return;
            }

            SkillType type;

            switch (skill)
            {
                case "p": case "power":
                case "pow": case "damage": case "dmg":
                    type = SkillType.Dmg;
                    break;
                case "g": case "grit": case "defense": case "def":
                    type = SkillType.Def;
                    break;
                case "f": case "focus": case "luck": case "critchance": case "crit":
                    type = SkillType.Crit;
                    break;
                default:
                    await ctx.ReplyAsync("That's not a valid skill name! You can choose power, grit or focus.");
                    return;
            }

            if (Game(ctx).player.spentSkill[type] + amount > RpgPlayer.SkillMax)
            {
                await ctx.ReplyAsync($"A skill line can only have {RpgPlayer.SkillMax} skill points invested.");
            }

            int oldValue = Game(ctx).player.spentSkill[type];
            Game(ctx).player.spentSkill[type] += amount;
            Game(ctx).player.skillPoints -= amount;
            await SaveGameAsync(ctx);
            await ctx.AutoReactAsync();

            var newSkills = RpgExtensions.SkillTypes.Values
                .Where(x => x.Type == type && x.SkillGet > oldValue && x.SkillGet <= Game(ctx).player.spentSkill[x.Type]);

            foreach (var sk in newSkills)
            {
                await ctx.RespondAsync("You unlocked a new skill!\n\n" +
                    $"**[{sk.Name}]**" +
                    $"\n*{sk.Description}*" +
                    $"\nMana cost: {sk.ManaCost}{CustomEmoji.Mana}" +
                    $"\nUse with the command: `{ctx.Prefix}rpg {sk.Shortcut}`");
            }

            if (Game(ctx).State == GameState.Active && !Game(ctx).IsPvp)
            {
                Game(ctx).lastEmote = RpgGame.SkillsEmote;
                Game(ctx).fightEmbed = Game(ctx).player.Skills(ctx.Prefix, true);
                await UpdateGameMessageAsync(ctx);
            }
        }


        [Command("name"), Aliases("setname")]
        [Description("Set your character's name")]
        public async Task SetPlayerName(CommandContext ctx, [RemainingText] string name)
        {
            if (Game(ctx) is null)
            {
                await ctx.ReplyAsync($"You haven't started your adventure! Use `{Storage.GetPrefix(ctx)}rpg start` to create a character.");
                return;
            }

            var msg = ctx.Message;

            if (string.IsNullOrWhiteSpace(name))
            {
                await ctx.ReplyAsync("Say your hero's new name:");

                msg = await ctx.GetResponseAsync();
                if (msg is null)
                {
                    await ctx.ReplyAsync("Timed out 💨");
                    return;
                }

                name = msg.Content;
                if (string.IsNullOrWhiteSpace(name)) return;
            }

            if (name.Length > RpgGame.NameCharLimit)
            {
                await ctx.ReplyAsync("Your name can't be longer than 32 characters.");
                return;
            };

            Game(ctx).player.SetName(name);
            await SaveGameAsync(ctx);
            await msg.AutoReactAsync();
        }


        [Command("color"), Aliases("setcolor")]
        [Description("Set your character's color, displayed in embeds")]
        public async Task SetPlayerColor(CommandContext ctx, [RemainingText] string colorString)
        {
            if (Game(ctx) is null)
            {
                await ctx.ReplyAsync($"You haven't started your adventure! Use `{Storage.GetPrefix(ctx)}rpg start` to create a character.");
                return;
            }

            if (string.IsNullOrWhiteSpace(colorString))
            {
                await ctx.ReplyAsync("Say the name or hex code of your new color:");

                var response = await ctx.GetResponseAsync(60);
                if (response is null)
                {
                    await ctx.ReplyAsync("Timed out 💨");
                    return;
                }

                colorString = response.Content;
                if (string.IsNullOrWhiteSpace(colorString)) return;
            }

            var color = colorString.ToColor();

            if (color is null)
            {
                await ctx.ReplyAsync($"{CustomEmoji.Cross} That is neither a valid color name or hex code. Example: `red` or `#FF0000`");
                return;
            }

            Game(ctx).player.Color = color.Value;
            await SaveGameAsync(ctx);

            await ctx.RespondAsync(new DiscordEmbedBuilder
            {
                Title = "Player color set",
                Description = $"#{color.Value.Value:X6}",
                Color = color.Value,
            });
        }


        [Command("cancel"), Aliases("kill", "die", "surrender")]
        [Description("Surrender and lose the current battle")]
        public async Task CancelBattle(CommandContext ctx)
        {
            if (Game(ctx) is null)
            {
                await ctx.ReplyAsync($"You haven't started your adventure! Use `{Storage.GetPrefix(ctx)}rpg start` to create a character.");
                return;
            }

            if (Game(ctx).State != GameState.Active)
            {
                await ctx.ReplyAsync("You're not fighting anything.");
            }

            string reply;

            await DeleteGameMessageAsync(ctx);

            if (Game(ctx).IsPvp)
            {
                reply = "PVP match cancelled.";

                if (Game(ctx).PvpBattleConfirmed) Game(ctx).PvpGame.ResetBattle(GameState.Completed);
            }
            else
            {
                reply = Game(ctx).player.Die();
            }

            Game(ctx).ResetBattle(GameState.Completed);

            await ctx.RespondAsync(reply);
        }


        [Command("pvp"), Aliases("vs", "challenge")]
        [Description("Starts a PVP battle with another user")]
        public async Task StartPvpBattle(CommandContext ctx, DiscordMember otherMember)
        {
            if (Game(ctx) is null)
            {
                await ctx.ReplyAsync($"You haven't started your adventure! Use `{Storage.GetPrefix(ctx)}rpg start` to create a character.");
                return;
            }

            if (Game(ctx).State == GameState.Active)
            {
                await ctx.ReplyAsync("You're already busy fighting.");
                return;
            }

            if (otherMember is null)
            {
                await ctx.ReplyAsync("Can't find that user to challenge!");
                return;
            }
            if (otherMember.Id == ctx.User.Id)
            {
                await ctx.ReplyAsync("You can't battle yourself, smart guy.");
                return;
            }

            var otherGame = Games.GetForUser<RpgGame>(otherMember.Id);
            if (otherGame is null)
            {
                await ctx.ReplyAsync("This person doesn't have a hero.");
                return;
            }
            if (otherGame.State == GameState.Active)
            {
                await ctx.ReplyAsync("This person is already busy fighting.");
                return;
            }

            Game(ctx).StartFight(otherMember.Id);
            Game(ctx).isPvpTurn = false;

            string content = $"{otherMember.Mention} You are being challenged to a PVP battle.\n" +
                             $"Send \"accept\" to accept the challenge, or \"cancel\" to deny.\n" +
                             $"You should heal first!";

            Game(ctx).fightEmbed = await Game(ctx).FightPvPAsync();
            var msg = await RespondGameAsync(ctx, content);

            var response = await Input.GetResponseAsync(x =>
                x.Channel.Id == ctx.Channel.Id && x.Author.Id == otherMember.Id
                && new[] { "accept", "cancel" }.Contains(x.Content.ToLowerInvariant()), 120);

            if (Game(ctx).pvpUserId != otherMember.Id) return; // Cancelled by challenger before getting a response

            if (response is null)
            {
                Game(ctx).ResetBattle(GameState.Cancelled);

                try { await msg.ModifyAsync("Timed out 💨", null); }
                catch (NotFoundException) { await ctx.ReplyAsync("Timed out 💨"); }
            }
            else if (response.Content.Equals("cancel", StringComparison.OrdinalIgnoreCase))
            {
                Game(ctx).ResetBattle(GameState.Cancelled);

                try { await msg.ModifyAsync("Battle cancelled ⚔", null); }
                catch (NotFoundException) { await ctx.ReplyAsync("Battle cancelled ⚔"); }

                await response.AutoReactAsync();
            }
            else
            {
                otherGame.StartFight(ctx.User.Id);
                otherGame.isPvpTurn = true;
                otherGame.ChannelId = Game(ctx).ChannelId;
                otherGame.MessageId = Game(ctx).MessageId;
                await Games.SaveAsync(Game(ctx));
                await Games.SaveAsync(otherGame);

                await response.AutoReactAsync();

                Game(ctx).fightEmbed = await Game(ctx).FightPvPAsync();
                otherGame.fightEmbed = Game(ctx).fightEmbed;

                msg = await UpdateGameMessageAsync(ctx);
                await AddBattleEmotes(msg, Game(ctx));
            }
        }


        [Command("start")]
        [Description("Creates your RPG character")]
        public async Task StartGame(CommandContext ctx)
        {
            if (Game(ctx) is not null)
            {
                await ctx.RespondAsync("You already have a hero!");
                return;
            }

            StartNewGame(new RpgGame(ctx.User.Username, ctx.User.Id, Services));

            await SendManual(ctx);
        }


        [Command("delete")]
        [Description("Deletes your RPG character permanently")]
        public async Task DeleteGame(CommandContext ctx)
        {
            if (Game(ctx) is null)
            {
                await ctx.ReplyAsync($"You haven't started your adventure! Use `{Storage.GetPrefix(ctx)}rpg start` to create a character.");
                return;
            }

            await ctx.ReplyAsync(
                $"❗ You're about to completely delete your progress in ReactionRPG.\n" +
                $"Are you sure you want to delete your level {Game(ctx).player.Level} hero? (Yes/No)");

            if (await ctx.GetYesResponseAsync() ?? false)
            {
                EndGame(ctx);
                await ctx.ReplyAsync("Hero deleted 💀");
            }
            else await ctx.ReplyAsync("Hero not deleted ⚔");
        }


        [Command("manual"), Aliases("help", "instructions")]
        [Description("Read the game's manual")]
        public async Task SendManual(CommandContext ctx)
        {
            var embed = new DiscordEmbedBuilder
            {
                Title = $"ReactionRPG Game Manual",
                Color = Colors.Black,
                Description =
                $"Welcome to ReactionRPG{$", {Game(ctx)?.player.Name}".If(Game(ctx) is not null)}!" +
                $"\nThis game consists of battling enemies, levelling up and unlocking skills." +
                $"\nYou can play in *any channel*, even in DMs with the bot." +
                $"\nUse the command **{ctx.Prefix}rpg help** for a list of commands." +
                $"\nUse **{ctx.Prefix}rpg profile** to see your hero's profile, and **{ctx.Prefix}rpg name/color** to personalize it.",
            };

            embed.AddField("⚔ Battles",
                $"To start a battle or re-send the current battle, use the command **{ctx.Prefix}rpg**" +
                $"\nWhen in a battle, you can use the _message reactions_ to perform an action." +
                $"\nSelect a number {RpgGame.EmoteNumberInputs[0]} of an enemy to attack. " +
                $"You can also select {RpgGame.MenuEmote} to inspect your enemies, " +
                $"and {RpgGame.ProfileEmote} to see your own profile and skills. React again to close these pages."
            );

            embed.AddField("📁 Utilities",
                $"You will get hurt in battle, and if you die you will lose EXP. To recover" +
                $" {CustomEmoji.Life}and {CustomEmoji.Mana}, use **{ctx.Prefix}rpg heal**" +
                $" - It can only be used once per battle." +
                $"\nYou will unlock equipment as you progress. When you have an item in your inventory," +
                $" you can equip it using **{ctx.Prefix}rpg equip [item]** - You can switch weapons at any time," +
                $" but you can't switch armors mid-battle."
            );

            embed.AddField("⭐ Skills",
                $"When you level up you gain __skill points__, which you can spend." +
                $"\nThere are three skill lines: __Power__ (attack), __Grit__ (defense) and __Focus__ (crit chance). " +
                $"\nYou can view your skills page using **{ctx.Prefix}rpg skills** - " +
                $"To spend points in a skill line use **{ctx.Prefix}rpg spend [skill] [amount]**\n" +
                $"You can unlock __active skills__, which can be used during battle and cost {CustomEmoji.Mana}. " +
                $"To use an active skill you unlocked, use that skill's command which can be found in the skills page."
            );

            await ctx.RespondAsync(embed);
        }



        private static async Task AddBattleEmotes(DiscordMessage message, RpgGame game)
        {
            if (game.IsPvp)
            {
                try { await message.CreateReactionAsync(RpgGame.PvpEmote); }
                catch (NotFoundException) { }
                return;
            }

            var emotes = game.IsPvp
                ? new[] { RpgGame.PvpEmote}
                : RpgGame.EmoteNumberInputs.Take(game.enemies.Count).Concat(RpgGame.EmoteOtherInputs);

            try
            {
                foreach (var emote in emotes)
                {
                    await message.CreateReactionAsync(emote);
                }
            }
            catch (NotFoundException) { }
        }
    }
}
