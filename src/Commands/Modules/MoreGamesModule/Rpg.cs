using System;
using System.Linq;
using System.Threading.Tasks;
using System.Reflection;
using System.Collections.Generic;
using Discord;
using Discord.Net;
using Discord.Commands;
using PacManBot.Games;
using PacManBot.Games.Concrete.Rpg;
using PacManBot.Constants;
using PacManBot.Extensions;
using Player = PacManBot.Games.Concrete.Rpg.Player;

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


        [Command("rpg"), Remarks("Play a generic RPG"), HideHelp]
        [Summary("Play an RPG in Discord. This is a beta test." +
            "\n\n**__Commands:__**" +
            "\n**{prefix}rpg manual** - See detailed instructions for the game." +
            "\n\n**{prefix}rpg battle** - Start a new battle or resend the current battle." +
            "\n**{prefix}rpg profile** - Check a summary of your hero." +
            "\n**{prefix}rpg skills** - Check your hero's skills lines and active skills." +
            "\n**{prefix}rpg equip [item]** - Equip an item in your inventory." +
            "\n**{prefix}rpg heal** - Refill your HP, only once per battle." +
            "\n**{prefix}rpg skill [shortcut]** - Use an active skill in battle." +
            "\n**{prefix}rpg spend [skill] [amount]** - Spend skill points on a skill line." +
            "\n\n**{prefix}rpg name [name]** - Change your hero's name." +
            "\n**{prefix}rpg color [color]** - Change the color of your hero's profile.")]
        [BetterRequireBotPermission(ChannelPermission.EmbedLinks | ChannelPermission.AddReactions)]
        public async Task GenericRpg(string commandName = "", [Remainder]string args = "")
        {
            commandName = commandName.ToLower();
            args = args.Trim();

            var command = RpgMethods
                .FirstOrDefault(x => x.GetCustomAttribute<RpgCommandAttribute>().Names.Contains(commandName));

            if (command == null)
            {
                await ReplyAsync($"Unknown RPG command! Do `{Prefix}rpg help` for help");
            }
            else
            {
                var game = Games.GetForUser<RpgGame>(Context.User.Id);
                if (game == null && command.GetCustomAttribute<NotRequiresRpgAttribute>() == null)
                {
                    await ReplyAsync($"You can use `{Prefix}rpg start` to start your adventure.");
                    return;
                }

                await (Task)command.Invoke(this, new object[] { game, args });
            }
        }




        [RpgCommand("", "profile", "stats", "inventory")]
        public async Task RpgProfile(RpgGame game, string args)
        {
            await ReplyAsync(game.player.Profile(Prefix));
        }


        [RpgCommand("skills", "spells")]
        public async Task RpgSkills(RpgGame game, string args)
        {
            await ReplyAsync(game.player.Skills(Prefix));
        }


        [RpgCommand("battle", "fight", "b")]
        public async Task RpgStartBattle(RpgGame game, string args)
        {
            if (game.State != State.Active)
            {
                var timeLeft = TimeSpan.FromMinutes(0.5) - (DateTime.Now - game.lastBattle);
                if (timeLeft > TimeSpan.Zero && !Config.debugRpg.Contains(Context.User.Id))
                {
                    await ReplyAsync($"{CustomEmoji.Cross} You may battle again in {timeLeft.Humanized(empty: "1 second")}");
                    return;
                }

                game.StartFight();
            }

            var old = await game.GetMessage();
            if (old != null)
            {
                try { await old.DeleteAsync(); }
                catch (HttpException) { }
            }

            var message = await ReplyAsync(game.Fight());
            game.ChannelId = Context.Channel.Id;
            game.MessageId = message.Id;

            Games.Save(game);

            await AddRpgEmotes(message, game.enemies.Count);
        }


        [RpgCommand("heal", "h", "potion")]
        public async Task RpgHeal(RpgGame game, string args)
        {
            if (game.lastHeal > game.lastBattle && game.State == State.Active)
            {
                await ReplyAsync($"{CustomEmoji.Cross} You already healed during this battle.");
                return;
            }

            var timeLeftH = TimeSpan.FromMinutes(3) - (DateTime.Now - game.lastHeal);
            if (timeLeftH > TimeSpan.Zero && !Config.debugRpg.Contains(Context.User.Id))
            {
                await ReplyAsync($"{CustomEmoji.Cross} You may heal again in {timeLeftH.Humanized(empty: "1 second")}");
                return;
            }

            game.lastHeal = DateTime.Now;
            int previousLife = game.player.Life;
            game.player.Life += (game.player.MaxLife * 0.75).Round();
            Games.Save(game);

            if (game.player.Life < game.player.MaxLife) await ReplyAsync($"💟 Healed by {game.player.Life - previousLife}!");
            else await ReplyAsync($"💟 Healed to full HP!");

            if (game.State == State.Active && Context.BotCan(ChannelPermission.ManageMessages))
                await Context.Message.DeleteAsync(DefaultOptions);
        }


        [RpgCommand("equip", "e")]
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
                    await ReplyAsync("You can change weapons mid-battle, but you can't change armor mid-battle.");
                    return;
                }

                game.player.EquipItem(bestMatch.Key);
                Games.Save(game);
                await ReplyAsync($"⚔ Equipped `{bestMatch}`.");

                if (game.State == State.Active && Context.BotCan(ChannelPermission.ManageMessages))
                    await Context.Message.DeleteAsync(DefaultOptions);
            }
            else
            {
                await ReplyAsync($"Can't find a weapon with that name in your inventory. "
                    + $"Did you mean `{bestMatch}`?".If(bestPercent > 0.39));
            }
        }


        [RpgCommand("skill", "s")]
        public async Task RpgUseActiveSkill(RpgGame game, string args)
        {
            if (args == "")
            {
                await ReplyAsync("Please specify an active skill shortcut to use that skill.");
                return;
            }
            if (game.State != State.Active)
            {
                await ReplyAsync("You can only use an active skill during battle!");
                return;
            }

            var unlocked = PacManBot.Games.Concrete.Rpg.Extensions.SkillTypes.Values
                .Where(s => s.SkillGet <= game.player.spentSkill[s.Type]);

            if (unlocked.Count() == 0)
            {
                await ReplyAsync("You haven't unlocked any active skills to use.");
                return;
            }

            var skill = unlocked.FirstOrDefault(s => s.Shortcut == args.Trim().ToLower());

            if (skill == null)
            {
                await ReplyAsync("Invalid skill shortcut." +
                    $"\nYour unlocked skills are: {unlocked.Select(x => $"`{x}`").JoinString(", ")}");
                return;
            }
            if (skill.ManaCost > game.player.Mana)
            {
                await ReplyAsync($"You only have {game.player.Mana} mana, but {skill.Name} requires {skill.ManaCost}");
                return;
            }


            game.player.Mana -= skill.ManaCost;
            game.fightEmbed = game.Fight(-1, skill);

            var message = await game.GetMessage();
            if (message == null || game.ChannelId != Context.Channel.Id)
            {
                message = await ReplyAsync(game.fightEmbed);
                game.ChannelId = Context.Channel.Id;
                game.MessageId = message.Id;
                Games.Save(game);

                await AddRpgEmotes(message, game.enemies.Count);
            }
            else
            {
                await message.ModifyAsync(game.GetMessageUpdate());
            }

            if (Context.BotCan(ChannelPermission.ManageMessages)) await Context.Message.DeleteAsync(DefaultOptions);
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
            string[] splice = args.Split(' ');
            string skill = splice[0];
            if (splice.Length < 2 || !int.TryParse(splice[1], out int amount) || amount < 1)
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
                case "p": case "power": case "damage": case "dmg":
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

            if (game.player.spentSkill[type] + amount > Player.LevelCap)
            {
                await ReplyAsync($"A skill line can only have {Player.LevelCap} skill points invested.");
                return;
            }

            game.player.spentSkill[type] += amount;
            game.player.skillPoints -= amount;
            await AutoReactAsync();
            Games.Save(game);
        }


        [RpgCommand("name", "setname")]
        public async Task RpgSetName(RpgGame game, string args)
        {
            if (args == "") await ReplyAsync("Please specify a new name.");
            else if (args.Length > 32) await ReplyAsync("Your name can't be longer than 32 characters.");
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
            if (args == "") await ReplyAsync("Please specify a color name.");
            else
            {
                var colors = typeof(Color).GetFields().Where(x => x.FieldType == typeof(Color));

                FieldInfo bestMatch = null;
                double bestPercent = 0;
                foreach (var field in colors)
                {
                    double sim = args.Similarity(field.Name, false);
                    if (sim > bestPercent)
                    {
                        bestMatch = field;
                        bestPercent = sim;
                    }
                    if (sim == 1) break;
                }

                if (bestPercent > 0.59) 
                {
                    game.player.color = (Color)bestMatch.GetValue(null);
                    Games.Save(game);
                    await ReplyAsync($"{CustomEmoji.Check} Set player color to {bestMatch.Name}");
                }
                else
                {
                    await ReplyAsync("Can't find a color with that name. "
                        + $"Did you mean `{bestMatch.Name}`?".If(bestPercent > 0.34));
                }
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
                Title = $"Generic RPG Game Manual",
                Color = Colors.Black,
                Description =
                $"Welcome to Generic RPG{$", {game?.player.Name}".If(game != null)}!" +
                $"\nThe game consists of battling enemies, levelling up and unlocking skills." +
                $"\nUse the command **{Prefix}rpg help** for a list of commands." +
                $"\nUse **{Prefix}rpg** to see your hero's profile, and **{Prefix}rpg name/color** to personalize it.",
            };

            embed.AddField(new EmbedFieldBuilder
            {
                Name = "⚔ Battles",
                Value =
                $"To start a battle, use the command **{Prefix}rpg battle**" +
                $"\nWhen in a battle, you can use the _message reactions_ to perform an action." +
                $"\nSelect a number {RpgGame.EmoteNumberInputs[0]} of an enemy to attack. " +
                $"You can also select {RpgGame.MenuEmote} to inspect your enemies," +
                $"and {RpgGame.ProfileEmote} to see your own profile.",
            });

            embed.AddField(new EmbedFieldBuilder
            {
                Name = "📁 Utilities",
                Value =
                $"You will get hurt in battle, and if you die you will lose EXP. To recover HP and Mana, " +
                $"use **{Prefix}rpg heal** - It can only be used once per battle." +
                $"\nYou will unlock equipment as you progress. When you have an item in your inventory," +
                $" you can equip it using **{Prefix}rpg equip [item]** - You can switch weapons at any time," +
                $" but you can't switch armors mid-battle.",
            });

            embed.AddField(new EmbedFieldBuilder
            {
                Name = "⭐ Skills",
                Value =
                $"When you level up you gain __skill points__ which you can spend." +
                $"\nThere are three skill lines: __Power__ (attack), __Grit__ (defense) and __Focus__ (crit chance). " +
                $"\nYou can view your skill lines and active skills using **{Prefix}rpg skills** - " +
                $"To spend points in a skill line use **{Prefix}rpg spend [skill] [amount]**\n" +
                $"You can unlock __active skills__, which can be used during battle and cost mana. " +
                $"To use an active skill, do **{Prefix}rpg skill [shortcut]** with the skill's shortcut.",
            });

            await ReplyAsync(embed);
        }



        private static async Task AddRpgEmotes(IUserMessage message, int enemyCount)
        {
            var emotes = RpgGame.EmoteNumberInputs.Take(enemyCount).Concat(RpgGame.EmoteOtherInputs);
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
