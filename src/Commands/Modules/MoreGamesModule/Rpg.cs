using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Net;
using PacManBot.Constants;
using PacManBot.Extensions;
using PacManBot.Games;
using PacManBot.Games.Concrete;
using PacManBot.Games.Concrete.Rpg;

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


            private static readonly Type ReturnType = typeof(Task<string>);
            private static readonly IEnumerable<Type> ParameterTypes = new[] { typeof(RpgGame), typeof(string) };

            // Runtime check that all commands are valid
            public object VerifyMethod(MethodInfo method)
            {
                if (method.ReturnType != ReturnType || !method.GetParameters().Select(x => x.ParameterType).SequenceEqual(ParameterTypes))
                {
                    throw new InvalidOperationException($"{method.Name} does not match the expected {GetType().Name} signature.");
                }
                return this;
            }
        }

        [AttributeUsage(AttributeTargets.Method)]
        private class NotRequiresRpgAttribute : Attribute
        {
        }


        private static readonly IEnumerable<MethodInfo> RpgMethods = typeof(MoreGamesModule).GetMethods()
            .Where(x => x.Get<RpgCommandAttribute>()?.VerifyMethod(x) != null)
            .ToArray();




        [Command("rpg"), Remarks("Play an RPG game"), Parameters("[command]"), Priority(4)]
        [Summary("Play ReactionRPG, a new game where you beat monsters and level up." +
            "\nThe game is yours. You can play in **any channel** anywhere you go, even DMs with the bot." +
            "\n\n**__Commands:__**" +
            "\n**{prefix}rpg manual** - See detailed instructions for the game." +
            "\n\n**{prefix}rpg** - Start a new battle or resend the current battle." +
            "\n**{prefix}rpg pvp <player>** - Challenge a user to a battle, or accept a user's challenge." +
            "\n**{prefix}rpg equip <item>** - Equip an item in your inventory." +
            "\n**{prefix}rpg heal** - Refill your HP, only once per battle." +
            "\n**{prefix}rpg cancel** - Cancel a battle, dying instantly against monsters." +
            "\n\n**{prefix}rpg profile** - Check a summary of your hero (or another person's)." +
            "\n**{prefix}rpg skills** - Check your hero's skills lines and active skills." +
            "\n**{prefix}rpg spend <skill> <amount>** - Spend skill points on a skill line." +
            "\n**{prefix}rpg name <name>** - Change your hero's name." +
            "\n**{prefix}rpg color <color>** - Change the color of your hero's profile." +
            "\n**{prefix}rpg delete** - Delete your hero.")]
        public async Task RpgMaster(string commandName = "", [Remainder]string args = "")
        {
            commandName = commandName.ToLower();
            args = args.Trim();

            var game = Games.GetForUser<RpgGame>(Context.User.Id);

            var command = RpgMethods
                .FirstOrDefault(x => x.Get<RpgCommandAttribute>().Names.Contains(commandName));

            if (command == null)
            {
                var skill = game == null ? null
                    : RpgExtensions.SkillTypes.Values.FirstOrDefault(x => x.Shortcut == commandName);

                if (skill == null)
                {
                    await ReplyAsync($"Unknown RPG command! Do `{Context.Prefix}rpg manual` for game instructions," +
                        $" or `{Context.Prefix}rpg help` for a list of commands.");
                    return;
                }

                string response = await RpgUseActiveSkill(game, skill);
                if (response != null) await ReplyAsync(response);
            }
            else
            {
                if (game != null) game.LastPlayed = DateTime.Now;

                if (game == null && command.Get<NotRequiresRpgAttribute>() == null)
                {
                    await ReplyAsync($"You can use `{Context.Prefix}rpg start` to start your adventure.");
                    return;
                }

                string response = await command.Invoke<Task<string>>(this, game, args);
                if (response != null) await ReplyAsync(response);
            }
        }




        [RpgCommand("", "battle", "fight", "b", "rpg")]
        public async Task<string> RpgStartBattle(RpgGame game, string args)
        {
            if (game.State != State.Active)
            {
                var timeLeft = TimeSpan.FromSeconds(30) - (DateTime.Now - game.lastBattle);
                if (timeLeft > TimeSpan.Zero)
                {
                    return $"{CustomEmoji.Cross} You may battle again in {timeLeft.Humanized(empty: "1 second")}";
                }

                game.StartFight();
                game.fightEmbed = game.Fight();
            }

            game.CancelRequests();

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

            await RpgAddEmotes(message, game);
            return null;
        }


        public async Task<string> RpgUseActiveSkill(RpgGame game, Skill skill)
        {
            if (game.State != State.Active)
                return "You can only use an active skill during battle!";
            if (game.IsPvp && !game.isPvpTurn)
                return "It's not your turn.";

            var unlocked = game.player.UnlockedSkills;

            if (!game.player.UnlockedSkills.Contains(skill))
                return $"You haven't unlocked the `{skill.Shortcut}` active skill.";
            if (game.player.Mana == 0)
                return $"You don't have any {CustomEmoji.Mana}left! You should heal.";
            if (skill.ManaCost > game.player.Mana)
                return $"{skill.Name} requires {skill.ManaCost}{CustomEmoji.Mana}" +
                       $"but you only have {game.player.Mana}{CustomEmoji.Mana}";

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

                await RpgAddEmotes(gameMsg, game);
            }
            else
            {
                Games.Save(game);
                game.CancelRequests();
                try { await gameMsg.ModifyAsync(game.GetMessageUpdate(), game.GetRequestOptions()); }
                catch (OperationCanceledException) { }
            }

            if (Context.BotCan(ChannelPermission.ManageMessages)) await Context.Message.DeleteAsync(DefaultOptions);

            return null;
        }




        [RpgCommand("profile", "p", "stats", "inventory", "inv"), NotRequiresRpg]
        public async Task<string> RpgProfile(RpgGame game, string args)
        {
            var otherUser = await Context.ParseUserAsync(args);
            if (otherUser != null) game = Games.GetForUser<RpgGame>(otherUser.Id);

            if (game == null)
            {
                if (otherUser == null) await ReplyAsync($"You can use `{Context.Prefix}rpg start` to start your adventure."); 
                else await ReplyAsync("This person hasn't started their adventure.");
                return null;
            }

            await ReplyAsync(game.player.Profile(Context.Prefix, own: otherUser == null));
            return null;
        }


        [RpgCommand("skills", "skill", "s", "spells")]
        public async Task<string> RpgSkills(RpgGame game, string args)
        {
            await ReplyAsync(game.player.Skills(Context.Prefix));
            return null;
        }



        [RpgCommand("heal", "h", "potion")]
        public async Task<string> RpgHeal(RpgGame game, string args)
        {
            if (game.lastHeal > game.lastBattle && game.State == State.Active)
                return $"{CustomEmoji.Cross} You already healed during this battle.";
            else if (game.IsPvp && game.PvpBattleConfirmed)
                return $"{CustomEmoji.Cross} You can't heal in a PVP battle.";

            var timeLeft = TimeSpan.FromMinutes(5) - (DateTime.Now - game.lastHeal);

            if (timeLeft > TimeSpan.Zero)
                return $"{CustomEmoji.Cross} You may heal again in {timeLeft.Humanized(empty: "1 second")}";

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
                    game.CancelRequests();
                    try { await message.ModifyAsync(m => m.Embed = game.fightEmbed.Build(), game.GetRequestOptions()); }
                    catch (OperationCanceledException) { }
                }

                if (Context.BotCan(ChannelPermission.ManageMessages))
                {
                    await Context.Message.DeleteAsync(DefaultOptions);
                }
            }

            return null;
        }


        [RpgCommand("equip", "e", "weapon", "armor")]
        public async Task<string> RpgEquip(RpgGame game, string args)
        {
            if (args == "") return "You must specify an item from your inventory.";

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

            if (bestPercent < 0.69)
                return $"Can't find a weapon with that name in your inventory." +
                       $" Did you mean `{bestMatch}`?".If(bestPercent > 0.39);
            if (bestMatch is Armor && game.State == State.Active)
                return "You can't switch armors mid-battle (but you can switch weapons).";

            game.player.EquipItem(bestMatch.Key);
            Games.Save(game);
            await ReplyAsync($"⚔ Equipped `{bestMatch}`.");

            if (game.State == State.Active && !game.IsPvp)
            {
                var message = await game.GetMessage();
                if (message != null)
                {
                    game.lastEmote = RpgGame.ProfileEmote;
                    var embed = game.player.Profile(Context.Prefix, reaction: true).Build();
                    game.CancelRequests();
                    try { await message.ModifyAsync(m => m.Embed = embed, game.GetRequestOptions()); }
                    catch (OperationCanceledException) { }
                }

                if (Context.BotCan(ChannelPermission.ManageMessages))
                {
                    await Context.Message.DeleteAsync(DefaultOptions);
                }
            }

            return null;
        }


        [RpgCommand("spend", "invest")]
        public async Task<string> RpgSpendSkills(RpgGame game, string args)
        {
            if (args == "") return "Please specify a skill and amount to spend.";

            args = args.ToLower();
            string[] splice = args.Split(' ', 2);
            string skill = splice[0];
            int amount = 0;
            if (splice.Length == 2)
            {
                if (splice[1] == "all") amount = game.player.skillPoints;
                else int.TryParse(splice[1], out amount); // Default value 0
            }

            if (amount < 1) return "Please specify a valid amount of skill points to spend.";
            if (amount > game.player.skillPoints) return "You don't have that many skill points!";

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
                    return "That's not a valid skill name! You can choose power, grit or focus.";
            }

            if (game.player.spentSkill[type] + amount > RpgPlayer.SkillMax)
                return $"A skill line can only have {RpgPlayer.SkillMax} skill points invested.";

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
                    $"\nUse with the command: `{Context.Prefix}rpg {sk.Shortcut}`");
            }

            if (game.State == State.Active && !game.IsPvp)
            {
                var message = await game.GetMessage();
                if (message != null)
                {
                    game.lastEmote = RpgGame.SkillsEmote;
                    var embed = game.player.Skills(Context.Prefix, true).Build();
                    game.CancelRequests();
                    try { await message.ModifyAsync(m => m.Embed = embed, game.GetRequestOptions()); }
                    catch (OperationCanceledException) { }
                }
            }

            return null;
        }


        [RpgCommand("name", "setname")]
        public async Task<string> RpgSetName(RpgGame game, string args)
        {
            if (args == "") return "Please specify a new name.";
            if (args.Length > 32) return "Your name can't be longer than 32 characters.";
            if (args.Contains("@")) return $"Your name can't contain \"@\"";

            game.player.SetName(args);
            Games.Save(game);
            await AutoReactAsync();
            return null;
        }


        [RpgCommand("color", "setcolor")]
        public async Task<string> RpgSetColor(RpgGame game, string args)
        {
            if (args == "") return "Please specify a color name.";

            var color = args.ToColor();

            if (color == null) return "That is neither a valid color name or hex code. Example: `red` or `#FF0000`";

            game.player.Color = color.Value;
            Games.Save(game);

            await ReplyAsync(new EmbedBuilder
            {
                Title = "Player color set",
                Description = $"#{color.Value.RawValue:X6}",
                Color = color,
            });
            return null;
        }


        [RpgCommand("cancel", "die", "end", "killme")]
        public async Task<string> RpgCancelBattle(RpgGame game, string args)
        {
            if (game.State != State.Active) return "You're not fighting anything.";

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
                game.CancelRequests();
                try { await oldMessage.DeleteAsync(); }
                catch (HttpException) { }
            }

            return reply;
        }


        [RpgCommand("pvp", "vs", "challenge")]
        public async Task<string> RpgStartPvpBattle(RpgGame game, string args)
        {
            if (game.State == State.Active) return "You're already busy fighting.";
            if (args == "") return "You must specify a person to challenge in a PVP battle.";

            RpgGame otherGame = null;
            var otherUser = await Context.ParseUserAsync(args);

            if (otherUser == null) return "Can't find that user to challenge!";
            if (otherUser.Id == Context.User.Id) return "You can't battle yourself, smart guy.";
            if ((otherGame = Games.GetForUser<RpgGame>(otherUser.Id)) == null) return "This person doesn't have a hero.";

            if (otherGame.pvpUserId == Context.User.Id) // Accept fight
            {
                game.StartFight(otherUser.Id);
                game.isPvpTurn = true;
                game.ChannelId = otherGame.ChannelId;
                game.MessageId = otherGame.MessageId;
                Games.Save(game);
                await AutoReactAsync();

                var msg = await otherGame.GetMessage();
                game.fightEmbed = game.FightPvP();
                game.CancelRequests();
                game.PvpGame.CancelRequests();
                try
                {
                    await msg.ModifyAsync(game.GetMessageUpdate(), game.GetRequestOptions());
                    await RpgAddEmotes(msg, game);
                }
                catch (OperationCanceledException) { }
            }
            else if (otherGame.State == State.Active)
            {
                return "This person is already busy fighting.";
            }
            else // Propose fight
            {
                game.StartFight(otherUser.Id);
                game.isPvpTurn = false;
                string content = $"{otherUser.Mention} do **{Context.Prefix}rpg pvp {Context.User.Mention}** " +
                                 $"to accept the challenge. You should heal first.";

                var msg = await ReplyAsync(content, game.FightPvP());
                game.MessageId = msg.Id;
                game.ChannelId = Context.Channel.Id;
                Games.Save(game);
            }

            return null;
        }


        [RpgCommand("start"), NotRequiresRpg]
        public async Task<string> RpgStart(RpgGame game, string args)
        {
            if (game != null) return "You already have a hero!";

            game = new RpgGame(Context.User.Username, Context.User.Id, Services);
            Games.Add(game);
            Games.Save(game);

            await RpgSendManual(game, "");
            return null;
        }


        [RpgCommand("delete")]
        public async Task<string> RpgDelete(RpgGame game, string args)
        {
            await ReplyAsync(
                $"❗ You're about to completely delete your progress in ReactionRPG.\n" +
                $"Are you sure you want to delete your level {game.player.Level} hero? (Yes/No)");

            if (await GetYesResponse())
            {
                Games.Remove(game);
                return "Hero deleted 💀";
            }
            return "Hero not deleted ⚔";
        }


        [RpgCommand("help", "commands"), NotRequiresRpg]
        public async Task<string> RpgSendHelp(RpgGame game, string args)
        {
            await ReplyAsync(Commands.GetCommandHelp("rpg"));
            return null;
        }


        [RpgCommand("manual", "instructions"), NotRequiresRpg]
        public async Task<string> RpgSendManual(RpgGame game, string args)
        {
            var embed = new EmbedBuilder
            {
                Title = $"ReactionRPG Game Manual",
                Color = Colors.Black,
                Description =
                $"Welcome to ReactionRPG{$", {game?.player.Name}".If(game != null)}!" +
                $"\nThis game consists of battling enemies, levelling up and unlocking skills." +
                $"\nYou can play in *any channel*, even in DMs with the bot." +
                $"\nUse the command **{Context.Prefix}rpg help** for a list of commands." +
                $"\nUse **{Context.Prefix}rpg profile** to see your hero's profile, and **{Context.Prefix}rpg name/color** to personalize it.",
            };

            embed.AddField(new EmbedFieldBuilder
            {
                Name = "⚔ Battles",
                Value =
                $"To start a battle or re-send the current battle, use the command **{Context.Prefix}rpg**" +
                $"\nWhen in a battle, you can use the _message reactions_ to perform an action." +
                $"\nSelect a number {RpgGame.EmoteNumberInputs[0]} of an enemy to attack. " +
                $"You can also select {RpgGame.MenuEmote} to inspect your enemies, " +
                $"and {RpgGame.ProfileEmote} to see your own profile and skills. React again to close these pages.",
            });

            embed.AddField(new EmbedFieldBuilder
            {
                Name = "📁 Utilities",
                Value =
                $"You will get hurt in battle, and if you die you will lose EXP. To recover" +
                $" {CustomEmoji.Life}and {CustomEmoji.Mana}, use **{Context.Prefix}rpg heal**" +
                $" - It can only be used once per battle." +
                $"\nYou will unlock equipment as you progress. When you have an item in your inventory," +
                $" you can equip it using **{Context.Prefix}rpg equip [item]** - You can switch weapons at any time," +
                $" but you can't switch armors mid-battle.",
            });

            embed.AddField(new EmbedFieldBuilder
            {
                Name = "⭐ Skills",
                Value =
                $"When you level up you gain __skill points__, which you can spend." +
                $"\nThere are three skill lines: __Power__ (attack), __Grit__ (defense) and __Focus__ (crit chance). " +
                $"\nYou can view your skills page using **{Context.Prefix}rpg skills** - " +
                $"To spend points in a skill line use **{Context.Prefix}rpg spend [skill] [amount]**\n" +
                $"You can unlock __active skills__, which can be used during battle and cost {CustomEmoji.Mana}. " +
                $"To use an active skill you unlocked, use that skill's command which can be found in the skills page.",
            });

            await ReplyAsync(embed);
            return null;
        }



        private static async Task RpgAddEmotes(IUserMessage message, RpgGame game)
        {
            if (game.IsPvp)
            {
                try { await message.AddReactionAsync(RpgGame.PvpEmote.ToEmoji(), DefaultOptions); }
                catch (HttpException) { }
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
            catch (HttpException) { }
        }
    }
}
