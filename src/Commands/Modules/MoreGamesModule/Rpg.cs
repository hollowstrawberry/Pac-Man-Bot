using System;
using System.Linq;
using System.Threading.Tasks;
using System.Reflection;
using System.Collections.Generic;
using Discord;
using Discord.Net;
using Discord.Commands;
using PacManBot.Games;
using PacManBot.Games.Concrete;
using PacManBot.Games.Concrete.Rpg;
using PacManBot.Constants;
using PacManBot.Extensions;

namespace PacManBot.Commands.Modules
{
    partial class MoreGamesModule
    {
        [AttributeUsage(AttributeTargets.Method)]
        private class RpgCommandAttribute : Attribute
        {
            public string[] Names { get; }
            public RpgCommandAttribute(params string[] names)
            {
                Names = names;
            }
        }

        [AttributeUsage(AttributeTargets.Method)]
        private class NotRequiresRpgAttribute : Attribute
        {
        }


        private static readonly IEnumerable<MethodInfo> RpgMethods = typeof(MoreGamesModule).GetMethods()
            .Where(x => x.GetCustomAttribute<RpgCommandAttribute>() != null)
            .ToList();




        [Command("rpg"), Remarks("Play an RPG game"), Parameters("[command]"), Priority(4)]
        [Summary("Play ReactionRPG, a new game where you beat monsters and level up." +
            "\n\n**__Commands:__**" +
            "\n**{prefix}rpg manual** - See detailed instructions for the game." +
            "\n\n**{prefix}rpg** - Start a new battle or resend the current battle." +
            "\n**{prefix}rpg pvp <player>** - Challenge a user to a battle, or accept a user's challenge." +
            "\n**{prefix}rpg equip <item>** - Equip an item in your inventory." +
            "\n**{prefix}rpg heal** - Refill your HP, only once per battle." +
            "\n**{prefix}rpg cancel** - Cancel a battle, dying instantly against monsters." +
            "\n\n**{prefix}rpg profile** - Check a summary of your hero." +
            "\n**{prefix}rpg skills** - Check your hero's skills lines and active skills." +
            "\n**{prefix}rpg spend <skill> <amount>** - Spend skill points on a skill line." +
            "\n**{prefix}rpg name <name>** - Change your hero's name." +
            "\n**{prefix}rpg color <color>** - Change the color of your hero's profile." +
            "\n**{prefix}rpg reset** - Delete your hero.")]
        public async Task RpgMaster(string commandName = "", [Remainder]string args = "")
        {
            commandName = commandName.ToLower();
            args = args.Trim();

            var game = Games.GetForUser<RpgGame>(Context.User.Id);

            var command = RpgMethods
                .FirstOrDefault(x => x.GetCustomAttribute<RpgCommandAttribute>().Names.Contains(commandName));

            if (command == null)
            {
                var skill = game == null ? null
                    : RpgExtensions.SkillTypes.Values.FirstOrDefault(x => x.Shortcut == commandName);

                if (skill == null)
                {
                    await ReplyAsync($"Unknown RPG command! Do `{Prefix}rpg manual` for game instructions," +
                        $" or `{Prefix}rpg help` for a list of commands.");
                    return;
                }

                await RpgUseActiveSkill(game, skill);
            }
            else
            {
                if (game == null && command.GetCustomAttribute<NotRequiresRpgAttribute>() == null)
                {
                    await ReplyAsync($"You can use `{Prefix}rpg start` to start your adventure.");
                    return;
                }

                await (Task)command.Invoke(this, new object[] { game, args });
            }
        }




        [RpgCommand("", "battle", "fight", "b", "rpg")]
        public async Task RpgStartBattle(RpgGame game, string args)
        {
            if (game.State != State.Active)
            {
                var timeLeft = TimeSpan.FromSeconds(30) - (DateTime.Now - game.lastBattle);
                if (timeLeft > TimeSpan.Zero)
                {
                    await ReplyAsync($"{CustomEmoji.Cross} You may battle again in {timeLeft.Humanized(empty: "1 second")}");
                    return;
                }

                game.StartFight();
                game.fightEmbed = game.Fight();
            }

            var old = await game.GetMessage();
            if (old != null)
            {
                try { await old.DeleteAsync(); }
                catch (HttpException) { }
            }

            game.fightEmbed = game.fightEmbed ?? (game.IsPvp ? game.FightPvP() : game.Fight());
            var message = await ReplyAsync(game.fightEmbed);

            game.ChannelId = Context.Channel.Id;
            game.MessageId = message.Id;
            if (game.IsPvp && game.PvpBattleConfirmed)
            {
                game.PvpGame.ChannelId = Context.Channel.Id;
                game.PvpGame.MessageId = message.Id;
            }

            Games.Save(game);

            await AddRpgEmotes(message, game);
        }


        public async Task RpgUseActiveSkill(RpgGame game, Skill skill)
        {
            if (game.State != State.Active)
            {
                await ReplyAsync("You can only use an active skill during battle!");
                return;
            }
            if (game.IsPvp && !game.isPvpTurn)
            {
                await ReplyAsync("It's not your turn.");
                return;
            }

            var unlocked = game.player.UnlockedSkills;

            if (!game.player.UnlockedSkills.Contains(skill))
            {
                await ReplyAsync($"You haven't unlocked the `{skill.Shortcut}` active skill.");
                return;
            }
            if (game.player.Mana == 0)
            {
                await ReplyAsync($"You don't have any {CustomEmoji.Mana}left! You should heal.");
                return;
            }
            if (skill.ManaCost > game.player.Mana)
            {
                await ReplyAsync($"{skill.Name} requires {skill.ManaCost}{CustomEmoji.Mana}" +
                    $"but you only have {game.player.Mana}{CustomEmoji.Mana}");
                return;
            }

            game.player.UpdateStats();
            foreach (var op in game.Opponents) op.UpdateStats();

            var gameMsg = await game.GetMessage();
            game.player.Mana -= skill.ManaCost;
            if (game.IsPvp)
            {
                game.fightEmbed = game.FightPvP(true, skill);
                if (game.IsPvp) // Match didn't end
                {
                    game.isPvpTurn = false;
                    game.PvpGame.isPvpTurn = true;
                    game.PvpGame.fightEmbed = game.fightEmbed;
                }
            }
            else
            {
                game.fightEmbed = game.Fight(-1, skill);
            }

            if (game.State == State.Active && (gameMsg == null || gameMsg.Channel.Id != Context.Channel.Id))
            {
                gameMsg = await ReplyAsync(game.fightEmbed);
                game.ChannelId = Context.Channel.Id;
                game.MessageId = gameMsg.Id;
                Games.Save(game);

                await AddRpgEmotes(gameMsg, game);
            }
            else
            {
                Games.Save(game);
                await gameMsg.ModifyAsync(game.GetMessageUpdate());
            }

            if (Context.BotCan(ChannelPermission.ManageMessages)) await Context.Message.DeleteAsync(DefaultOptions);
        }




        [RpgCommand("profile", "p", "stats", "inventory", "inv")]
        public async Task RpgProfile(RpgGame game, string args)
        {
            await ReplyAsync(game.player.Profile(Prefix));
        }


        [RpgCommand("skills", "skill", "s", "spells")]
        public async Task RpgSkills(RpgGame game, string args)
        {
            await ReplyAsync(game.player.Skills(Prefix));
        }



        [RpgCommand("heal", "h", "potion")]
        public async Task RpgHeal(RpgGame game, string args)
        {
            if (game.lastHeal > game.lastBattle && game.State == State.Active)
            {
                await ReplyAsync($"{CustomEmoji.Cross} You already healed during this battle.");
                return;
            }
            else if (game.IsPvp && game.PvpBattleConfirmed)
            {
                await ReplyAsync($"{CustomEmoji.Cross} You can't heal in a PVP battle.");
                return;
            }

            var timeLeft = TimeSpan.FromMinutes(5) - (DateTime.Now - game.lastHeal);
            if (timeLeft > TimeSpan.Zero)
            {
                await ReplyAsync($"{CustomEmoji.Cross} You may heal again in {timeLeft.Humanized(empty: "1 second")}");
                return;
            }

            game.lastHeal = DateTime.Now;
            game.player.Life = game.player.MaxLife;
            game.player.Mana = game.player.MaxMana;
            Games.Save(game);

            await ReplyAsync($"💟 Fully restored!");

            if (game.State == State.Active)
            {
                var message = await game.GetMessage();
                if (message != null)
                {
                    game.lastEmote = "";
                    game.fightEmbed = game.IsPvp ? game.FightPvP() : game.Fight();
                    try { await message.ModifyAsync(m => m.Embed = game.fightEmbed.Build(), game.GetRequestOptions()); }
                    catch { }
                }

                if (Context.BotCan(ChannelPermission.ManageMessages))
                {
                    await Context.Message.DeleteAsync(DefaultOptions);
                }
            }
        }


        [RpgCommand("equip", "e", "weapon", "armor")]
        public async Task RpgEquip(RpgGame game, string args)
        {
            if (args == "")
            {
                await ReplyAsync("You must specify an item from your inventory.");
                return;
            }

            Equipment bestMatch = null;
            double bestPercent = 0;
            foreach (var item in game.player.inventory.Select(x => x.GetEquip()))
            {
                double sim = args.Similarity(item.Name, false);
                if (sim > bestPercent)
                {
                    bestMatch = item;
                    bestPercent = sim;
                }
                if (sim == 1) break;
            }


            if (bestPercent > 0.69)
            {
                if (bestMatch is Armor && game.State == State.Active)
                {
                    await ReplyAsync("You can't switch armors mid-battle (but you can switch weapons).");
                    return;
                }

                game.player.EquipItem(bestMatch.Key);
                Games.Save(game);
                await ReplyAsync($"⚔ Equipped `{bestMatch}`.");

                if (game.State == State.Active && !game.IsPvp)
                {
                    var message = await game.GetMessage();
                    if (message != null)
                    {
                        game.lastEmote = RpgGame.ProfileEmote;
                        try { await message.ModifyAsync(m => m.Embed = game.player.Profile().Build(), game.GetRequestOptions()); }
                        catch { }
                    }

                    if (Context.BotCan(ChannelPermission.ManageMessages))
                    {
                        await Context.Message.DeleteAsync(DefaultOptions);
                    }
                }
            }
            else
            {
                await ReplyAsync($"Can't find a weapon with that name in your inventory. "
                    + $"Did you mean `{bestMatch}`?".If(bestPercent > 0.39));
            }
        }


        [RpgCommand("spend", "invest")]
        public async Task RpgSendSkills(RpgGame game, string args)
        {
            if (args == "")
            {
                await ReplyAsync("Please specify a skill and amount to spend.");
                return;
            }

            args = args.ToLower();
            string[] splice = args.Split(' ', 2);
            string skill = splice[0];
            int amount = 0;
            if (splice.Length == 2)
            {
                if (splice[1] == "all") amount = game.player.skillPoints;
                else
                {
                    int.TryParse(splice[1], out amount);
                }
            }
            if (amount < 1)
            {
                await ReplyAsync("Please specify a valid amount of skill points to spend.");
                return;
            }
            if (amount > game.player.skillPoints)
            {
                await ReplyAsync("You don't have that many skill points!");
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
                    await ReplyAsync("That's not a valid skill name! You can choose power, grit or focus.");
                    return;
            }

            if (game.player.spentSkill[type] + amount > RpgPlayer.SkillMax)
            {
                await ReplyAsync($"A skill line can only have {RpgPlayer.SkillMax} skill points invested.");
                return;
            }

            int oldValue = game.player.spentSkill[type];
            game.player.spentSkill[type] += amount;
            game.player.skillPoints -= amount;
            Games.Save(game);
            await AutoReactAsync();

            var newSkills = RpgExtensions.SkillTypes.Values
                .Where(x => x.Type == type && x.SkillGet > oldValue && x.SkillGet <= game.player.spentSkill[x.Type]);

            foreach (var sk in newSkills)
            {
                await ReplyAsync("You unlocked a new skill!\n\n" +
                    $"**[{sk.Name}]**" +
                    $"\n*{sk.Description}*" +
                    $"\nMana cost: {sk.ManaCost}{CustomEmoji.Mana}" +
                    $"\nUse with the command: `{Prefix}rpg {sk.Shortcut}`");
            }
        }


        [RpgCommand("name", "setname")]
        public async Task RpgSetName(RpgGame game, string args)
        {
            if (args == "") await ReplyAsync("Please specify a new name.");
            else if (args.Length > 32) await ReplyAsync("Your name can't be longer than 32 characters.");
            else if (args.Contains("@")) await ReplyAsync($"Your name can't contain \"@\"");
            else
            {
                game.player.SetName(args);
                Games.Save(game);
                await AutoReactAsync();
            }
        }


        [RpgCommand("color", "setcolor")]
        public async Task RpgSetColor(RpgGame game, string args)
        {
            if (args == "")
            {
                await ReplyAsync("Please specify a color name.");
                return;
            }

            var color = args.ToColor();
            if (color == null)
            {
                await ReplyAsync("That is neither a valid color name or hex code. Example: `red` or `#FF0000`");
            }
            else
            {
                game.player.Color = color.Value;
                Games.Save(game);
                var embed = new EmbedBuilder
                {
                    Title = "Player color set",
                    Description = $"#{color.Value.RawValue:X}",
                    Color = color,
                };
                await ReplyAsync(embed);
            }
        }


        [RpgCommand("cancel", "die", "end", "killme")]
        public async Task RpgCancelBattle(RpgGame game, string args)
        {
            if (game.State != State.Active)
            {
                await ReplyAsync("You're not fighting anything.");
                return;
            }

            string reply = "";
            var oldMessage = await game.GetMessage();

            if (game.IsPvp)
            {
                reply = "PVP match cancelled.";
                if (game.PvpBattleConfirmed) game.PvpGame.ResetBattle(State.Completed);
            }
            else
            {
                reply = game.player.Die();
            }

            game.ResetBattle(State.Completed);

            if (oldMessage != null)
            {
                try { await oldMessage.DeleteAsync(); }
                catch (HttpException) { }
            }

            await ReplyAsync(reply);
        }


        [RpgCommand("pvp", "vs", "challenge")]
        public async Task RpgStartPvpBattle(RpgGame game, string args)
        {
            if (game.State == State.Active)
            {
                await ReplyAsync("You're already busy fighting.");
                return;
            }
            if (args == "")
            {
                await ReplyAsync("You must specify a person to challenge in a PVP battle.");
                return;
            }

            RpgGame otherGame = null;
            var otherUser = await Context.ParseUserAsync(args);
            if (otherUser == null)
            {
                await ReplyAsync("Can't find that user to challenge!");
                return;
            }
            if ((otherGame = Games.GetForUser<RpgGame>(otherUser.Id)) == null)
            {
                await ReplyAsync("This person doesn't have a hero.");
                return;
            }


            if (otherGame.pvpUserId == Context.User.Id) // Accept fight
            {
                game.StartFight(otherUser.Id);
                game.isPvpTurn = true;
                game.ChannelId = otherGame.ChannelId;
                game.MessageId = otherGame.MessageId;
                Games.Save(game);
                await AutoReactAsync();

                var msg = await otherGame.GetMessage();
                try
                {
                    await AddRpgEmotes(msg, game);
                    await msg.ModifyAsync(x => {
                        x.Content = "";
                        x.Embed = game.FightPvP().Build();
                    }, game.GetRequestOptions());
                }
                catch { }
            }
            else if (otherGame.State == State.Active)
            {
                await ReplyAsync("This person is already busy fighting.");
            }
            else // Propose fight
            {
                game.StartFight(otherUser.Id);
                game.isPvpTurn = false;
                string content = $"{otherUser.Mention} do **{Prefix}rpg pvp {Context.User.Mention}** " +
                                 $"to accept the challenge. You should heal first.";

                var msg = await ReplyAsync(content, game.FightPvP());
                game.MessageId = msg.Id;
                game.ChannelId = Context.Channel.Id;
                Games.Save(game);
            }
        }


        [RpgCommand("start"), NotRequiresRpg]
        public async Task RpgStart(RpgGame game, string args)
        {
            if (game != null)
            {
                await ReplyAsync("You already have a hero!");
                return;
            }

            game = new RpgGame(Context.User.Username, Context.User.Id, Services);
            Games.Add(game);
            Games.Save(game);

            await SendRpgManual(game, "");
        }


        [RpgCommand("reset")]
        public async Task RpgDelete(RpgGame game, string args)
        {
            if (args?.SanitizeMarkdown() == game.player.Name)
            {
                Games.Remove(game);
                await ReplyAsync($"{game.player.Name}'s adventure has ended.");
            }
            else
            {
                await ReplyAsync(
                    $"❗ You're about to completely delete your progress in ReactionRPG. This is not reversible." +
                    $"\nDo **{Prefix}rpg reset {game.player.Name}** if you're sure you want to end your adventure.");
            }
        }


        [RpgCommand("help", "commands"), NotRequiresRpg]
        public async Task RpgSendHelp(RpgGame game, string args)
        {
            await ReplyAsync(Help.MakeHelp("rpg"));
        }


        [RpgCommand("manual", "instructions"), NotRequiresRpg]
        public async Task SendRpgManual(RpgGame game, string args)
        {
            var embed = new EmbedBuilder
            {
                Title = $"ReactionRPG Game Manual",
                Color = Colors.Black,
                Description =
                $"Welcome to ReactionRPG{$", {game?.player.Name}".If(game != null)}!" +
                $"\nThis game consists of battling enemies, levelling up and unlocking skills." +
                $"\nUse the command **{Prefix}rpg help** for a list of commands." +
                $"\nUse **{Prefix}rpg profile** to see your hero's profile, and **{Prefix}rpg name/color** to personalize it.",
            };

            embed.AddField(new EmbedFieldBuilder
            {
                Name = "⚔ Battles",
                Value =
                $"To start a battle, use the command **{Prefix}rpg**" +
                $"\nWhen in a battle, you can use the _message reactions_ to perform an action." +
                $"\nSelect a number {RpgGame.EmoteNumberInputs[0]} of an enemy to attack. " +
                $"You can also select {RpgGame.MenuEmote} to inspect your enemies, " +
                $"and {RpgGame.ProfileEmote} to see your own profile; react again to close these pages.",
            });

            embed.AddField(new EmbedFieldBuilder
            {
                Name = "📁 Utilities",
                Value =
                $"You will get hurt in battle, and if you die you will lose EXP. To recover" +
                $" {CustomEmoji.Life}and {CustomEmoji.Mana}, use **{Prefix}rpg heal**" +
                $" - It can only be used once per battle." +
                $"\nYou will unlock equipment as you progress. When you have an item in your inventory," +
                $" you can equip it using **{Prefix}rpg equip [item]** - You can switch weapons at any time," +
                $" but you can't switch armors mid-battle.",
            });

            embed.AddField(new EmbedFieldBuilder
            {
                Name = "⭐ Skills",
                Value =
                $"When you level up you gain __skill points__, which you can spend." +
                $"\nThere are three skill lines: __Power__ (attack), __Grit__ (defense) and __Focus__ (crit chance). " +
                $"\nYou can view your skills page using **{Prefix}rpg skills** - " +
                $"To spend points in a skill line use **{Prefix}rpg spend [skill] [amount]**\n" +
                $"You can unlock __active skills__, which can be used during battle and cost {CustomEmoji.Mana}. " +
                $"To use an active skill you unlocked, use that skill's command which can be found in the skills page.",
            });

            await ReplyAsync(embed);
        }



        private static async Task AddRpgEmotes(IUserMessage message, RpgGame game)
        {
            if (game.IsPvp)
            {
                try { await message.AddReactionAsync(RpgGame.PvpEmote.ToEmoji(), DefaultOptions); }
                catch { }
                return;
            }

            var emotes = game.IsPvp
                ? new[] { RpgGame.PvpEmote}
                : RpgGame.EmoteNumberInputs.Take(game.enemies.Count()).Concat(RpgGame.EmoteOtherInputs);

            try
            {
                foreach (var emote in emotes)
                {
                    await message.AddReactionAsync((IEmote)emote.ToEmote() ?? emote.ToEmoji(), DefaultOptions);
                }
            }
            catch { }
        }
    }
}
