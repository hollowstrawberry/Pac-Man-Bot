using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Net;
using Discord.Commands;
using PacManBot.Games;
using PacManBot.Games.Concrete.RPG;
using PacManBot.Constants;
using PacManBot.Extensions;

namespace PacManBot.Commands.Modules
{
    partial class MoreGamesModule
    {
        [Command("rpg"), Remarks("Play a generic RPG"), HideHelp]
        [Summary("Play an RPG. This is an alpha test." +
            "\n\n**__Commands:__**" +
            "\n**{prefix}rpg battle** - Start a new battle or resend the current battle." +
            "\n**{prefix}rpg profile** - Check your hero." +
            "\n**{prefix}rpg equip [weapon]** - Equip a weapon in your inventory." +
            "\n**{prefix}rpg heal** - Refill your HP." +
            "\n\n**{prefix}rpg name [name]** - Change your hero's name." +
            "\n**{prefix}rpg color [color]** - Change the color of your hero's profile.")]
        [BetterRequireBotPermission(ChannelPermission.EmbedLinks | ChannelPermission.AddReactions)]
        public async Task GenericRpg(string command = "", [Remainder]string args = "")
        {
            command = command.ToLower();
            args = args.Trim();

            var game = Games.GetForUser<RpgGame>(Context.User.Id);
            if (game == null)
            {
                if (command != "start")
                {
                    await ReplyAsync($"You can use `{Prefix}rpg start` to start your adventure.");
                    return;
                }

                game = new RpgGame(Context.User.Username, Context.User.Id, Services);
                Games.Add(game);

                var embed = new EmbedBuilder
                {
                    Title = "Welcome to Generic RPG! This is an alpha test.",
                    Description =
                    "**Instructions:**" +
                    $"\nUse the command **{Prefix}rpg profile** to see your new hero." +
                    $"\nThe game consists of battling enemies and levelling up." +
                    $"\nTo start a battle, use the command **{Prefix}rpg battle**" +
                    $"\nWhen in a battle, you can use _message reactions_ to perform an action." +
                    $"\nSelect the number {RpgGame.EmoteNumberInputs[0]} of an enemy to attack." +
                    $"\nYou can also select {RpgGame.MenuEmote} to inspect your enemies," +
                    $"and {RpgGame.ProfileEmote} to see your own profile." +
                    $"\n\nUse the command **{Prefix}rpg help** for more commands and information." +
                    $"\nGood luck!",

                    Color = Colors.Black,
                };

                await ReplyAsync(embed);
                return;
            }

            switch (command)
            {
                case "fight":
                case "battle":
                    if (game.State != State.Active)
                    {
                        var timeLeft = TimeSpan.FromMinutes(0.5) - (DateTime.Now - game.lastBattle);
                        if (timeLeft > TimeSpan.Zero)
                        {
                            await ReplyAsync($"You may battle again in {timeLeft.Humanized()}");
                            return;
                        }

                        game.StartFight();
                        Games.Save(game);
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

                    var emotes = RpgGame.EmoteNumberInputs.Take(game.enemies.Count).Concat(RpgGame.EmoteOtherInputs);
                    try
                    {
                        foreach (var emote in emotes)
                        {
                            await message.AddReactionAsync((IEmote)emote.ToEmote() ?? emote.ToEmoji(), DefaultOptions);
                        }
                    }
                    catch { }

                    break;


                case "equip":
                case "weapon":
                    var item = game.player.inventory.Select(x => x.GetWeapon()).FirstOrDefault(x => x.Equals(args));
                    if (item == null) await ReplyAsync("That weapon is not in your inventory.");
                    else
                    {
                        game.player.EquipWeapon(item.Key);
                        Games.Save(game);
                        await AutoReactAsync();
                    }
                    break;


                case "heal":
                case "potion":
                case "hp":
                    if (game.lastHeal > game.lastBattle && game.State == State.Active)
                    {
                        await ReplyAsync($"{CustomEmoji.Cross} You already healed during this battle.");
                        return;
                    }

                    var timeLeftH = TimeSpan.FromMinutes(5) - (DateTime.Now - game.lastHeal);
                    if (timeLeftH > TimeSpan.Zero)
                    {
                        await ReplyAsync($"{CustomEmoji.Cross} You can heal again in {timeLeftH.Humanized()}");
                        return;
                    }

                    game.lastHeal = DateTime.Now;
                    game.player.Life += 50;
                    await AutoReactAsync();
                    break;


                case "profile":
                case "stats":
                case "skills":
                case "me":
                case "":
                    await ReplyAsync(game.player.Profile());
                    break;


                case "help":
                    await ReplyAsync(Help.MakeHelp("rpg"));
                    break;


                case "name":
                    if (args == "") await ReplyAsync("Please specify a new name.");
                    else if (args.Length > 32) await ReplyAsync("Your name can't be longer than 32 characters.");
                    else
                    {
                        game.player.SetName(args);
                    }
                    break;


                case "color":
                    if (args == "") await ReplyAsync("Please specify a new name.");
                    else
                    {
                        var color = (Color)typeof(Color).GetFields()
                            .FirstOrDefault(x => x.Name.ToLower() == args.ToLower().Replace(" ", ""))
                            .GetValue(null);

                        if (color == null) await ReplyAsync("Can't find that color.");
                        else
                        {
                            game.player.color = color;
                            await AutoReactAsync();
                        }
                    }
                    break;


                default:
                    await ReplyAsync($"Unknown RPG command. Do `{Prefix}rpg help` for help.");
                    break;
            }
        }
    }
}
