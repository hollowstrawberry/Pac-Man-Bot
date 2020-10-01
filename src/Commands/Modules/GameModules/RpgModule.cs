using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
    [Group(ModuleNames.Games), Description("3")]
    public class RpgModule : BaseGameModule<RpgGame>
    {
        private static readonly IEnumerable<MethodInfo> RpgMethods = typeof(RpgModule).GetMethods()
            .Where(x => x.Get<RpgCommandAttribute>()?.VerifyMethod(x) != null)
            .ToArray();

        [AttributeUsage(AttributeTargets.Method)]
        private class NotRequiresRpgAttribute : Attribute { }

        [AttributeUsage(AttributeTargets.Method)]
        private class RpgCommandAttribute : Attribute
        {
            public string[] Names { get; }
            public RpgCommandAttribute(params string[] names)
            {
                Names = names;
            }

            // Runtime check that all commands are valid
            public object VerifyMethod(MethodInfo method)
            {
                if (method.ReturnType != typeof(Task<string>) || method.GetParameters().Length != 2
                    || method.GetParameters().First().GetType() != typeof(CommandContext)
                    || method.GetParameters().Skip(1).First().GetType() != typeof(string))
                {
                    throw new InvalidOperationException($"{method.Name} does not match the expected {GetType().Name} signature.");
                }
                return this;
            }
        }



        [Command("rpg"), Priority(2)]
        [Description(
            "Play ReactionRPG, a new game where you beat monsters and level up." +
            "\nThe game is yours. You can play in **any channel** anywhere you go, even DMs with the bot." +
            "\n\n**__Commands:__**" +
            "\n**{prefix}rpg manual** - See detailed instructions for the game." +
            "\n\n**{prefix}rpg** - Start a new battle or resend the current battle." +
            "\n**{prefix}rpg pvp <player>** - Challenge a user to a battle!" +
            "\n**{prefix}rpg equip <item>** - Equip an item in your inventory." +
            "\n**{prefix}rpg heal** - Refill your HP, only once per battle." +
            "\n**{prefix}rpg cancel** - Cancel a battle, dying instantly against monsters." +
            "\n\n**{prefix}rpg profile** - Check a summary of your hero (or another person's)." +
            "\n**{prefix}rpg skills** - Check your hero's skills lines and active skills." +
            "\n**{prefix}rpg spend <skill> <amount>** - Spend skill points on a skill line." +
            "\n**{prefix}rpg name <name>** - Change your hero's name." +
            "\n**{prefix}rpg color <color>** - Change the color of your hero's profile." +
            "\n**{prefix}rpg delete** - Delete your hero.")]
        public async Task RpgMaster(CommandContext ctx, string commandName = "", [RemainingText]string args = "")
        {
            commandName = commandName.ToLowerInvariant();

            var command = RpgMethods
                .FirstOrDefault(x => x.Get<RpgCommandAttribute>().Names.Contains(commandName));

            if (command == null)
            {
                var skill = Game(ctx) == null ? null
                    : RpgExtensions.SkillTypes.Values.FirstOrDefault(x => x.Shortcut == commandName);

                if (skill == null)
                {
                    await ctx.RespondAsync($"Unknown RPG command! Do `{ctx.Prefix}rpg manual` for game instructions," +
                        $" or `{ctx.Prefix}rpg help` for a list of commands.");
                    return;
                }

                string response = await UseActiveSkill(ctx, skill);
                if (response != null) await ctx.RespondAsync(response);
            }
            else
            {
                if (Game(ctx) == null && command.Get<NotRequiresRpgAttribute>() == null)
                {
                    await ctx.RespondAsync($"You can use `{ctx.Prefix}rpg start` to start your adventure.");
                    return;
                }

                string response = await command.Invoke<Task<string>>(this, ctx, args.Trim());
                if (response != null) await ctx.RespondAsync(response);
            }
        }




        [RpgCommand("", "battle", "fight", "b", "rpg", "bump")]
        public async Task<string> Battle(CommandContext ctx, string arg)
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
                    return $"{CustomEmoji.Cross} You may battle again in {timeLeft.Humanized(empty: "1 second")}";
                }

                Game(ctx).StartFight();
                Game(ctx).fightEmbed = Game(ctx).Fight();
            }

            Game(ctx).fightEmbed = Game(ctx).fightEmbed ?? (Game(ctx).IsPvp ? Game(ctx).FightPvP() : Game(ctx).Fight());
            var msg = await RespondGameAsync(ctx);

            if (Game(ctx).IsPvp && Game(ctx).PvpBattleConfirmed)
            {
                Game(ctx).PvpGame.ChannelId = ctx.Channel.Id;
                Game(ctx).PvpGame.MessageId = msg.Id;
            }

            await SaveGameAsync(ctx);

            await AddBattleEmotes(msg, Game(ctx));
            return null;
        }


        public async Task<string> UseActiveSkill(CommandContext ctx, Skill skill)
        {
            if (Game(ctx).State != GameState.Active)
                return "You can only use an active skill during battle!";
            if (Game(ctx).IsPvp && !Game(ctx).isPvpTurn)
                return "It's not your turn.";

            var unlocked = Game(ctx).player.UnlockedSkills;

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
                Game(ctx).fightEmbed = Game(ctx).FightPvP(true, skill);
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
                if (msg == null || msg.Channel.Id != ctx.Channel.Id)
                {
                    msg = await RespondGameAsync(ctx);
                    await SaveGameAsync(ctx);
                    await AddBattleEmotes(msg, Game(ctx));
                }
                else
                {
                    await SaveGameAsync(ctx);
                    await UpdateGameMessageAsync(ctx);
                }
            }
            else
            {
                await SaveGameAsync(ctx);
                try { await msg.ModifyAsync(default, Game(ctx).fightEmbed.Build()); }
                catch (NotFoundException) { }
            }

            if (ctx.BotCan(Permissions.ManageMessages)) await ctx.Message.DeleteAsync();

            return null;
        }




        [RpgCommand("profile", "p", "stats", "inventory", "inv"), NotRequiresRpg]
        public async Task<string> SendProfile(CommandContext ctx, string arg)
        {
            var rpg = Game(ctx);
            var otherMember = (DiscordMember)await ctx.Client.GetCommandsNext().ConvertArgument<DiscordMember>(arg, ctx);
            if (otherMember != null) rpg = Games.GetForUser<RpgGame>(otherMember.Id);

            if (rpg == null)
            {
                if (otherMember == null) await ctx.RespondAsync($"You can use `{ctx.Prefix}rpg start` to start your adventure."); 
                else await ctx.RespondAsync("This person hasn't started their adventure.");
                return null;
            }

            await ctx.RespondAsync(rpg.player.Profile(ctx.Prefix, own: otherMember == null));
            return null;
        }


        [RpgCommand("skills", "skill", "s", "spells")]
        public async Task<string> SendSkills(CommandContext ctx, string arg)
        {
            await ctx.RespondAsync(Game(ctx).player.Skills(ctx.Prefix));
            return null;
        }



        [RpgCommand("heal", "h", "potion")]
        public async Task<string> HealPlayer(CommandContext ctx, string arg)
        {
            if (Game(ctx).lastHeal > Game(ctx).lastBattle && Game(ctx).State == GameState.Active)
                return $"{CustomEmoji.Cross} You already healed during this battle.";
            else if (Game(ctx).IsPvp && Game(ctx).PvpBattleConfirmed)
                return $"{CustomEmoji.Cross} You can't heal in a PVP battle.";

            var timeLeft = TimeSpan.FromMinutes(5) - (DateTime.Now - Game(ctx).lastHeal);

            if (timeLeft > TimeSpan.Zero)
                return $"{CustomEmoji.Cross} You may heal again in {timeLeft.Humanized(empty: "1 second")}";

            Game(ctx).lastHeal = DateTime.Now;
            Game(ctx).player.Life = Game(ctx).player.MaxLife;
            Game(ctx).player.Mana = Game(ctx).player.MaxMana;
            Game(ctx).player.Buffs.Clear();
            await SaveGameAsync(ctx);

            await ctx.RespondAsync($"💟 Fully restored!");

            if (Game(ctx).State == GameState.Active)
            {
                var message = await Game(ctx).GetMessageAsync();
                if (message != null)
                {
                    Game(ctx).lastEmote = "";
                    Game(ctx).fightEmbed = Game(ctx).IsPvp ? Game(ctx).FightPvP() : Game(ctx).Fight();
                    await UpdateGameMessageAsync(ctx);
                }

                if (ctx.BotCan(Permissions.ManageMessages))
                {
                    await ctx.Message.DeleteAsync();
                }
            }

            return null;
        }


        [RpgCommand("equip", "e", "weapon", "armor")]
        public async Task<string> EquipItem(CommandContext ctx, string arg)
        {
            if (arg == "") return "You must specify an item from your inventory.";

            Equipment bestMatch = null;
            double bestPercent = 0;
            foreach (var item in Game(ctx).player.inventory.Select(x => x.GetEquip()))
            {
                double sim = arg.Similarity(item.Name, false);
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
            if (bestMatch is Armor && Game(ctx).State == GameState.Active)
                return "You can't switch armors mid-battle (but you can switch weapons).";

            Game(ctx).player.EquipItem(bestMatch.Key);
            await SaveGameAsync(ctx);
            await ctx.RespondAsync($"⚔ Equipped `{bestMatch}`.");

            if (Game(ctx).State == GameState.Active && !Game(ctx).IsPvp)
            {
                Game(ctx).lastEmote = RpgGame.ProfileEmote;
                Game(ctx).fightEmbed = Game(ctx).player.Profile(ctx.Prefix, reaction: true);
                await SendOrUpdateGameMessageAsync(ctx);

                if (ctx.BotCan(Permissions.ManageMessages))
                {
                    await ctx.Message.DeleteAsync();
                }
            }

            return null;
        }


        [RpgCommand("spend", "invest")]
        public async Task<string> SpendSkillPoints(CommandContext ctx, string arg)
        {
            if (arg == "") return "Please specify a skill and amount to spend.";

            string[] splice = arg.ToLowerInvariant().Split(' ', 2);
            string skill = splice[0];
            int amount = 0;
            if (splice.Length == 2)
            {
                if (splice[1] == "all") amount = Game(ctx).player.skillPoints;
                else int.TryParse(splice[1], out amount); // Default value 0
            }

            if (amount < 1) return "Please specify a valid amount of skill points to spend.";
            if (amount > Game(ctx).player.skillPoints) return "You don't have that many skill points!";

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

            if (Game(ctx).player.spentSkill[type] + amount > RpgPlayer.SkillMax)
                return $"A skill line can only have {RpgPlayer.SkillMax} skill points invested.";

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
                await SendOrUpdateGameMessageAsync(ctx);
            }

            return null;
        }


        [RpgCommand("name", "setname")]
        public async Task<string> SetPlayerName(CommandContext ctx, string arg)
        {
            var msg = ctx.Message;
            string name = arg;

            if (name == "")
            {
                await ctx.RespondAsync("Say your hero's new name:");

                msg = await ctx.GetResponseAsync();
                if (msg == null) return "Timed out 💨";

                name = msg.Content;
                if (string.IsNullOrWhiteSpace(name)) return null;
            }

            if (name.Length > RpgGame.NameCharLimit) return "Your name can't be longer than 32 characters.";

            Game(ctx).player.SetName(name);
            await SaveGameAsync(ctx);
            await msg.AutoReactAsync();
            return null;
        }


        [RpgCommand("color", "setcolor")]
        public async Task<string> SetPlayerColor(CommandContext ctx, string arg)
        {
            if (arg == "")
            {
                await ctx.RespondAsync("Say the name or hex code of your new color:");

                var response = await ctx.GetResponseAsync(60);
                if (response == null) return "Timed out 💨";

                arg = response.Content;
                if (string.IsNullOrWhiteSpace(arg)) return null;
            }

            var color = arg.ToColor();

            if (color == null) return $"{CustomEmoji.Cross} That is neither a valid color name or hex code. " +
                                      $"Example: `red` or `#FF0000`";

            Game(ctx).player.Color = color.Value;
            await SaveGameAsync(ctx);

            await ctx.RespondAsync(new DiscordEmbedBuilder
            {
                Title = "Player color set",
                Description = $"#{color.Value.Value:X6}",
                Color = color.Value,
            });
            return null;
        }


        [RpgCommand("cancel", "die", "end", "killme")]
        public async Task<string> CancelBattle(CommandContext ctx, string arg)
        {
            if (Game(ctx).State != GameState.Active) return "You're not fighting anything.";

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

            return reply;
        }


        [RpgCommand("pvp", "vs", "challenge")]
        public async Task<string> StartPvpBattle(CommandContext ctx, string arg)
        {
            if (Game(ctx).State == GameState.Active) return "You're already busy fighting.";

            if (arg == "")
            {
                await ctx.RespondAsync("Specify the user you want to challenge:");
                var rsp = await ctx.GetResponseAsync();
                if (rsp == null) return "Timed out 💨";
                arg = rsp.Content;
            }

            var otherMember = (DiscordMember)await ctx.Client.GetCommandsNext().ConvertArgument<DiscordMember>(arg, ctx);
            if (otherMember == null) return "Can't find that user to challenge!";
            if (otherMember.Id == ctx.User.Id) return "You can't battle yourself, smart guy.";

            var otherGame = Games.GetForUser<RpgGame>(otherMember.Id);
            if (otherGame == null) return "This person doesn't have a hero.";
            if (otherGame.State == GameState.Active) return "This person is already busy fighting.";

            Game(ctx).StartFight(otherMember.Id);
            Game(ctx).isPvpTurn = false;

            string content = $"{otherMember.Mention} You are being challenged to a PVP battle.\n" +
                             $"Send \"accept\" to accept the challenge, or \"cancel\" to deny.\n" +
                             $"You should heal first!";

            Game(ctx).fightEmbed = Game(ctx).FightPvP();
            var msg = await RespondGameAsync(ctx, content);

            var response = await Input.GetResponseAsync(x =>
                x.Channel.Id == ctx.Channel.Id && x.Author.Id == otherMember.Id
                && new[] { "accept", "cancel" }.Contains(x.Content.ToLowerInvariant()), 120);

            if (Game(ctx).pvpUserId != otherMember.Id) return null; // Cancelled by challenger before getting a response

            if (response == null)
            {
                Game(ctx).ResetBattle(GameState.Cancelled);

                try { await msg.ModifyAsync("Timed out 💨", null); }
                catch (NotFoundException) { return "Timed out 💨"; }
                return null;
            }
            else if (response.Content.Equals("cancel", StringComparison.OrdinalIgnoreCase))
            {
                Game(ctx).ResetBattle(GameState.Cancelled);

                try { await msg.ModifyAsync("Battle cancelled ⚔", null); }
                catch (NotFoundException) { return "Battle cancelled ⚔"; }

                await response.AutoReactAsync();
                return null;
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

                Game(ctx).fightEmbed = Game(ctx).FightPvP();
                otherGame.fightEmbed = Game(ctx).fightEmbed;

                msg = await SendOrUpdateGameMessageAsync(ctx);
                await AddBattleEmotes(msg, Game(ctx));
            }

            return null;
        }


        [RpgCommand("start"), NotRequiresRpg]
        public async Task<string> StartGame(CommandContext ctx, string arg)
        {
            if (Game(ctx) != null) return "You already have a hero!";

            StartNewGame(new RpgGame(ctx.User.Username, ctx.User.Id, Services));

            await SendManual(ctx, arg);
            return null;
        }


        [RpgCommand("delete")]
        public async Task<string> DeleteGame(CommandContext ctx, string arg)
        {
            await ctx.RespondAsync(
                $"❗ You're about to completely delete your progress in ReactionRPG.\n" +
                $"Are you sure you want to delete your level {Game(ctx).player.Level} hero? (Yes/No)");

            if (await ctx.GetYesResponseAsync() ?? false)
            {
                EndGame(ctx);
                return "Hero deleted 💀";
            }
            return "Hero not deleted ⚔";
        }


        [RpgCommand("help", "commands"), NotRequiresRpg]
        public async Task<string> SendHelp(CommandContext ctx, string arg)
        {
            var desc = typeof(RpgModule).GetMethod(nameof(RpgMaster)).GetCustomAttribute<DescriptionAttribute>();
            await ctx.RespondAsync(desc.Description);
            return null;
        }


        [RpgCommand("manual", "instructions"), NotRequiresRpg]
        public async Task<string> SendManual(CommandContext ctx, string arg)
        {
            var embed = new DiscordEmbedBuilder
            {
                Title = $"ReactionRPG Game(ctx) Manual",
                Color = Colors.Black,
                Description =
                $"Welcome to ReactionRPG{$", {Game(ctx)?.player.Name}".If(Game(ctx) != null)}!" +
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
            return null;
        }



        private static async Task AddBattleEmotes(DiscordMessage message, RpgGame game)
        {
            if (game.IsPvp)
            {
                try { await message.CreateReactionAsync(RpgGame.PvpEmote.ToEmoji()); }
                catch (NotFoundException) { }
                return;
            }

            var emotes = game.IsPvp
                ? new[] { RpgGame.PvpEmote}
                : RpgGame.EmoteNumberInputs.Take(game.enemies.Count()).Concat(RpgGame.EmoteOtherInputs);

            try
            {
                foreach (var emote in emotes)
                {
                    await message.CreateReactionAsync(emote.ToEmoji());
                }
            }
            catch (NotFoundException) { }
        }
    }
}
