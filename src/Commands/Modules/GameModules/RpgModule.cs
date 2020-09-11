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

namespace PacManBot.Commands.Modules.GameModules
{
    [Name(ModuleNames.Games), Remarks("3")]
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
                if (method.ReturnType != typeof(Task<string>) || method.GetParameters().Length != 0)
                {
                    throw new InvalidOperationException($"{method.Name} does not match the expected {GetType().Name} signature.");
                }
                return this;
            }
        }


        public string ExtraArg { get; private set; }


        [Command("rpg"), Remarks("Play an RPG game"), Parameters("[command]"), Priority(2)]
        [Summary("Play ReactionRPG, a new game where you beat monsters and level up." +
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
        public async Task RpgMaster(string commandName = "", [Remainder]string args = "")
        {
            commandName = commandName.ToLowerInvariant();

            var command = RpgMethods
                .FirstOrDefault(x => x.Get<RpgCommandAttribute>().Names.Contains(commandName));

            if (command == null)
            {
                var skill = Game == null ? null
                    : RpgExtensions.SkillTypes.Values.FirstOrDefault(x => x.Shortcut == commandName);

                if (skill == null)
                {
                    await ReplyAsync($"Unknown RPG command! Do `{Context.Prefix}rpg manual` for game instructions," +
                        $" or `{Context.Prefix}rpg help` for a list of commands.");
                    return;
                }

                string response = await UseActiveSkill(skill);
                if (response != null) await ReplyAsync(response);
            }
            else
            {
                if (Game == null && command.Get<NotRequiresRpgAttribute>() == null)
                {
                    await ReplyAsync($"You can use `{Context.Prefix}rpg start` to start your adventure.");
                    return;
                }

                ExtraArg = args.Trim();
                string response = await command.Invoke<Task<string>>(this);
                if (response != null) await ReplyAsync(response);
            }
        }




        [RpgCommand("", "battle", "fight", "b", "rpg", "bump")]
        public async Task<string> Battle()
        {
            if (Game.State == GameState.Active)
            {
                await DeleteGameMessageAsync();
            }
            else
            {
                var timeLeft = TimeSpan.FromSeconds(30) - (DateTime.Now - Game.lastBattle);
                if (timeLeft > TimeSpan.Zero)
                {
                    return $"{CustomEmoji.Cross} You may battle again in {timeLeft.Humanized(empty: "1 second")}";
                }

                Game.StartFight();
                Game.fightEmbed = Game.Fight();
            }

            Game.fightEmbed = Game.fightEmbed ?? (Game.IsPvp ? Game.FightPvP() : Game.Fight());
            var msg = await ReplyGameAsync();

            if (Game.IsPvp && Game.PvpBattleConfirmed)
            {
                Game.PvpGame.ChannelId = Context.Channel.Id;
                Game.PvpGame.MessageId = msg.Id;
            }

            await SaveGameAsync();

            await AddBattleEmotes(msg, Game);
            return null;
        }


        public async Task<string> UseActiveSkill(Skill skill)
        {
            if (Game.State != GameState.Active)
                return "You can only use an active skill during battle!";
            if (Game.IsPvp && !Game.isPvpTurn)
                return "It's not your turn.";

            var unlocked = Game.player.UnlockedSkills;

            if (!Game.player.UnlockedSkills.Contains(skill))
                return $"You haven't unlocked the `{skill.Shortcut}` active skill.";
            if (Game.player.Mana == 0)
                return $"You don't have any {CustomEmoji.Mana}left! You should heal.";
            if (skill.ManaCost > Game.player.Mana)
                return $"{skill.Name} requires {skill.ManaCost}{CustomEmoji.Mana}" +
                       $"but you only have {Game.player.Mana}{CustomEmoji.Mana}";

            Game.player.UpdateStats();
            foreach (var op in Game.Opponents) op.UpdateStats();

            var msg = await Game.GetMessageAsync();
            Game.player.Mana -= skill.ManaCost;
            if (Game.IsPvp)
            {
                var otherGame = Game.PvpGame;
                Game.fightEmbed = Game.FightPvP(true, skill);
                if (Game.IsPvp) // Match didn't end
                {
                    Game.isPvpTurn = false;
                    otherGame.isPvpTurn = true;
                    otherGame.fightEmbed = Game.fightEmbed;
                }
                await Games.SaveAsync(otherGame);
            }
            else
            {
                Game.fightEmbed = Game.Fight(-1, skill);
            }

            if (Game.State == GameState.Active)
            {
                if (msg == null || msg.Channel.Id != Context.Channel.Id)
                {
                    msg = await ReplyGameAsync();
                    await SaveGameAsync();
                    await AddBattleEmotes(msg, Game);
                }
                else
                {
                    await SaveGameAsync();
                    await UpdateGameMessageAsync();
                }
            }
            else
            {
                await SaveGameAsync();
                try { await msg.ModifyAsync(x => x.Embed = Game.fightEmbed.Build(), Game.GetRequestOptions()); }
                catch (HttpException) { }
            }

            if (Context.BotCan(ChannelPermission.ManageMessages)) await Context.Message.DeleteAsync(DefaultOptions);

            return null;
        }




        [RpgCommand("profile", "p", "stats", "inventory", "inv"), NotRequiresRpg]
        public async Task<string> SendProfile()
        {
            var rpg = Game;
            var otherUser = await Context.ParseUserAsync(ExtraArg);
            if (otherUser != null) rpg = Games.GetForUser<RpgGame>(otherUser.Id);

            if (rpg == null)
            {
                if (otherUser == null) await ReplyAsync($"You can use `{Context.Prefix}rpg start` to start your adventure."); 
                else await ReplyAsync("This person hasn't started their adventure.");
                return null;
            }

            await ReplyAsync(rpg.player.Profile(Context.Prefix, own: otherUser == null));
            return null;
        }


        [RpgCommand("skills", "skill", "s", "spells")]
        public async Task<string> SendSkills()
        {
            await ReplyAsync(Game.player.Skills(Context.Prefix));
            return null;
        }



        [RpgCommand("heal", "h", "potion")]
        public async Task<string> HealPlayer()
        {
            if (Game.lastHeal > Game.lastBattle && Game.State == GameState.Active)
                return $"{CustomEmoji.Cross} You already healed during this battle.";
            else if (Game.IsPvp && Game.PvpBattleConfirmed)
                return $"{CustomEmoji.Cross} You can't heal in a PVP battle.";

            var timeLeft = TimeSpan.FromMinutes(5) - (DateTime.Now - Game.lastHeal);

            if (timeLeft > TimeSpan.Zero)
                return $"{CustomEmoji.Cross} You may heal again in {timeLeft.Humanized(empty: "1 second")}";

            Game.lastHeal = DateTime.Now;
            Game.player.Life = Game.player.MaxLife;
            Game.player.Mana = Game.player.MaxMana;
            Game.player.Buffs.Clear();
            await SaveGameAsync();

            await ReplyAsync($"💟 Fully restored!");

            if (Game.State == GameState.Active)
            {
                var message = await Game.GetMessageAsync();
                if (message != null)
                {
                    Game.lastEmote = "";
                    Game.fightEmbed = Game.IsPvp ? Game.FightPvP() : Game.Fight();
                    await UpdateGameMessageAsync();
                }

                if (Context.BotCan(ChannelPermission.ManageMessages))
                {
                    await Context.Message.DeleteAsync(DefaultOptions);
                }
            }

            return null;
        }


        [RpgCommand("equip", "e", "weapon", "armor")]
        public async Task<string> EquipItem()
        {
            if (ExtraArg == "") return "You must specify an item from your inventory.";

            Equipment bestMatch = null;
            double bestPercent = 0;
            foreach (var item in Game.player.inventory.Select(x => x.GetEquip()))
            {
                double sim = ExtraArg.Similarity(item.Name, false);
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
            if (bestMatch is Armor && Game.State == GameState.Active)
                return "You can't switch armors mid-battle (but you can switch weapons).";

            Game.player.EquipItem(bestMatch.Key);
            await SaveGameAsync();
            await ReplyAsync($"⚔ Equipped `{bestMatch}`.");

            if (Game.State == GameState.Active && !Game.IsPvp)
            {
                Game.lastEmote = RpgGame.ProfileEmote;
                Game.fightEmbed = Game.player.Profile(Context.Prefix, reaction: true);
                await SendOrUpdateGameMessageAsync();

                if (Context.BotCan(ChannelPermission.ManageMessages))
                {
                    await Context.Message.DeleteAsync(DefaultOptions);
                }
            }

            return null;
        }


        [RpgCommand("spend", "invest")]
        public async Task<string> SpendSkillPoints()
        {
            if (ExtraArg == "") return "Please specify a skill and amount to spend.";

            string[] splice = ExtraArg.ToLowerInvariant().Split(' ', 2);
            string skill = splice[0];
            int amount = 0;
            if (splice.Length == 2)
            {
                if (splice[1] == "all") amount = Game.player.skillPoints;
                else int.TryParse(splice[1], out amount); // Default value 0
            }

            if (amount < 1) return "Please specify a valid amount of skill points to spend.";
            if (amount > Game.player.skillPoints) return "You don't have that many skill points!";

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

            if (Game.player.spentSkill[type] + amount > RpgPlayer.SkillMax)
                return $"A skill line can only have {RpgPlayer.SkillMax} skill points invested.";

            int oldValue = Game.player.spentSkill[type];
            Game.player.spentSkill[type] += amount;
            Game.player.skillPoints -= amount;
            await SaveGameAsync();
            await AutoReactAsync();

            var newSkills = RpgExtensions.SkillTypes.Values
                .Where(x => x.Type == type && x.SkillGet > oldValue && x.SkillGet <= Game.player.spentSkill[x.Type]);

            foreach (var sk in newSkills)
            {
                await ReplyAsync("You unlocked a new skill!\n\n" +
                    $"**[{sk.Name}]**" +
                    $"\n*{sk.Description}*" +
                    $"\nMana cost: {sk.ManaCost}{CustomEmoji.Mana}" +
                    $"\nUse with the command: `{Context.Prefix}rpg {sk.Shortcut}`");
            }

            if (Game.State == GameState.Active && !Game.IsPvp)
            {
                Game.lastEmote = RpgGame.SkillsEmote;
                Game.fightEmbed = Game.player.Skills(Context.Prefix, true);
                await SendOrUpdateGameMessageAsync();
            }

            return null;
        }


        [RpgCommand("name", "setname")]
        public async Task<string> SetPlayerName()
        {
            var msg = Context.Message;
            string name = ExtraArg;

            if (name == "")
            {
                await ReplyAsync("Say your hero's new name:");

                msg = await GetResponseAsync();
                if (msg == null) return "Timed out 💨";

                name = msg.Content;
                if (string.IsNullOrWhiteSpace(name)) return null;
            }

            if (name.Length > RpgGame.NameCharLimit) return "Your name can't be longer than 32 characters.";

            Game.player.SetName(name);
            await SaveGameAsync();
            await msg.AutoReactAsync();
            return null;
        }


        [RpgCommand("color", "setcolor")]
        public async Task<string> SetPlayerColor()
        {
            if (ExtraArg == "")
            {
                await ReplyAsync("Say the name or hex code of your new color:");

                var response = await GetResponseAsync(60);
                if (response == null) return "Timed out 💨";

                ExtraArg = response.Content;
                if (string.IsNullOrWhiteSpace(ExtraArg)) return null;
            }

            var color = ExtraArg.ToColor();

            if (color == null) return $"{CustomEmoji.Cross} That is neither a valid color name or hex code. " +
                                      $"Example: `red` or `#FF0000`";

            Game.player.Color = color.Value;
            await SaveGameAsync();

            await ReplyAsync(new EmbedBuilder
            {
                Title = "Player color set",
                Description = $"#{color.Value.RawValue:X6}",
                Color = color,
            });
            return null;
        }


        [RpgCommand("cancel", "die", "end", "killme")]
        public async Task<string> CancelBattle()
        {
            if (Game.State != GameState.Active) return "You're not fighting anything.";

            string reply = "";

            await DeleteGameMessageAsync();

            if (Game.IsPvp)
            {
                reply = "PVP match cancelled.";

                if (Game.PvpBattleConfirmed) Game.PvpGame.ResetBattle(GameState.Completed);
            }
            else
            {
                reply = Game.player.Die();
            }

            Game.ResetBattle(GameState.Completed);

            return reply;
        }


        [RpgCommand("pvp", "vs", "challenge")]
        public async Task<string> StartPvpBattle()
        {
            if (Game.State == GameState.Active) return "You're already busy fighting.";

            if (ExtraArg == "")
            {
                await ReplyAsync("Specify the user you want to challenge:");
                var rsp = await GetResponseAsync();
                if (rsp == null) return "Timed out 💨";
                ExtraArg = rsp.Content;
            }

            var otherUser = await Context.ParseUserAsync(ExtraArg);
            if (otherUser == null) return "Can't find that user to challenge!";
            if (otherUser.Id == Context.User.Id) return "You can't battle yourself, smart guy.";

            var otherGame = Games.GetForUser<RpgGame>(otherUser.Id);
            if (otherGame == null) return "This person doesn't have a hero.";
            if (otherGame.State == GameState.Active) return "This person is already busy fighting.";

            Game.StartFight(otherUser.Id);
            Game.isPvpTurn = false;

            string content = $"{otherUser.Mention} You are being challenged to a PVP battle.\n" +
                             $"Send \"accept\" to accept the challenge, or \"cancel\" to deny.\n" +
                             $"You should heal first!";

            Game.fightEmbed = Game.FightPvP();
            var msg = await ReplyGameAsync(content);

            var response = await Input.GetResponseAsync(x =>
                x.Channel.Id == Context.Channel.Id && x.Author.Id == otherUser.Id
                && new[] { "accept", "cancel" }.Contains(x.Content.ToLowerInvariant()), 120);

            if (Game.pvpUserId != otherUser.Id) return null; // Cancelled by challenger before getting a response

            if (response == null)
            {
                Game.ResetBattle(GameState.Cancelled);

                try { await msg.ModifyAsync(x => { x.Content = "Timed out 💨"; x.Embed = null; }); }
                catch (HttpException) { return "Timed out 💨"; }
                return null;
            }
            else if (response.Content.Equals("cancel", StringComparison.OrdinalIgnoreCase))
            {
                Game.ResetBattle(GameState.Cancelled);

                try { await msg.ModifyAsync(x => { x.Content = "Battle cancelled ⚔"; x.Embed = null; }); }
                catch (HttpException) { return "Battle cancelled ⚔"; }

                await response.AutoReactAsync();
                return null;
            }
            else
            {
                otherGame.StartFight(Context.User.Id);
                otherGame.isPvpTurn = true;
                otherGame.ChannelId = Game.ChannelId;
                otherGame.MessageId = Game.MessageId;
                await Games.SaveAsync(Game);
                await Games.SaveAsync(otherGame);

                await response.AutoReactAsync();

                Game.fightEmbed = Game.FightPvP();
                otherGame.fightEmbed = Game.fightEmbed;
                otherGame.CancelRequests();
                otherGame.PvpGame.CancelRequests();

                msg = await SendOrUpdateGameMessageAsync();
                await AddBattleEmotes(msg, Game);
            }

            return null;
        }


        [RpgCommand("start"), NotRequiresRpg]
        public async Task<string> StartGame()
        {
            if (Game != null) return "You already have a hero!";

            StartNewGame(new RpgGame(Context.User.Username, Context.User.Id, Services));

            await SendManual();
            return null;
        }


        [RpgCommand("delete")]
        public async Task<string> DeleteGame()
        {
            await ReplyAsync(
                $"❗ You're about to completely delete your progress in ReactionRPG.\n" +
                $"Are you sure you want to delete your level {Game.player.Level} hero? (Yes/No)");

            if (await GetYesResponseAsync() ?? false)
            {
                EndGame();
                return "Hero deleted 💀";
            }
            return "Hero not deleted ⚔";
        }


        [RpgCommand("help", "commands"), NotRequiresRpg]
        public async Task<string> SendHelp()
        {
            await ReplyAsync(Commands.GetCommandHelp("rpg"));
            return null;
        }


        [RpgCommand("manual", "instructions"), NotRequiresRpg]
        public async Task<string> SendManual()
        {
            var embed = new EmbedBuilder
            {
                Title = $"ReactionRPG Game Manual",
                Color = Colors.Black,
                Description =
                $"Welcome to ReactionRPG{$", {Game?.player.Name}".If(Game != null)}!" +
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



        private static async Task AddBattleEmotes(IUserMessage message, RpgGame game)
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
