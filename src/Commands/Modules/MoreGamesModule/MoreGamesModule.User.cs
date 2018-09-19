using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Discord;
using Discord.Net;
using Discord.Commands;
using Discord.WebSocket;
using PacManBot.Constants;
using PacManBot.Extensions;
using PacManBot.Games.Concrete;

namespace PacManBot.Commands.Modules
{
    public partial class MoreGamesModule
    {
        [Command("rubik"), Alias("rubiks", "rubix", "rb", "rbx")]
        [Remarks("Your personal rubik's cube")]
        [Summary("Gives you a personal Rubik's Cube that you can take to any server or in DMs with the bot.\n\n__**Commands:**__" +
         "\n**{prefix}rubik [sequence]** - Execute a sequence of turns to apply on the cube." +
         "\n**{prefix}rubik moves** - Show notation help to control the cube." +
         "\n**{prefix}rubik scramble** - Scrambles the cube pieces completely." +
         "\n**{prefix}rubik reset** - Delete the cube, going back to its solved state." +
         "\n**{prefix}rubik showguide** - Toggle the help displayed below the cube. For pros.")]
        public async Task RubiksCube([Remainder] string input = "")
        {
            var cube = Games.GetForUser<RubiksGame>(Context.User.Id);

            if (cube == null)
            {
                cube = new RubiksGame(Context.Channel.Id, Context.User.Id, Services);
                Games.Add(cube);
            }

            bool removeOld = false;
            switch (input.ToLower())
            {
                case "moves":
                case "notation":
                    string help =
                        $"You can give a sequence of turns using the **{Prefix}rubik** command, " +
                        $"with turns separated by spaces.\nYou can do **{Prefix}rubik help** for a few more commands.\n\n" +
                        "**Simple turns:** U, D, L, R, F, B\nThese are the basic clockwise turns of the cube. " +
                        "They stand for the Up, Down, Left, Right, Front and Back sides.\n" +
                        "**Counterclockwise turns:** Add `'`. Example: U', R'\n" +
                        "**Double turns:** Add `2`. Example: F2, D2\n" +
                        "**Wide turns:** Add `w`. Example: Dw, Lw2, Uw'\n" +
                        "These rotate two layers at the same time in the direction of the given face.\n\n" +
                        "**Slice turns:** M E S\n" +
                        "These rotate the middle layer corresponding with L, D and B respectively.\n\n" +
                        "**Cube rotations:** x, y, z\n" +
                        "These rotate the entire cube in the direction of R, U and F respectively. " +
                        "They can also be counterclockwise or double.";

                    await ReplyAsync(help);
                    return;


                case "h":
                case "help":
                    await ReplyAsync(Help.MakeHelp("rubik", Prefix));
                    return;

                
                case "reset":
                case "solve":
                    Games.Remove(cube);
                    await AutoReactAsync();
                    return;


                case "scramble":
                case "shuffle":
                    cube.Scramble();
                    removeOld = true;
                    break;


                case "showguide":
                    cube.ShowHelp = !cube.ShowHelp;
                    if (cube.ShowHelp) await AutoReactAsync();
                    else await ReplyAsync("❗ You just disabled the help displayed below the cube.\n" +
                                          "Consider re-enabling it if you're not used to the game.");
                    break;


                default:
                    if (!string.IsNullOrEmpty(input))
                    {
                        if (!cube.TryDoMoves(input))
                        {
                            await ReplyAsync($"{CustomEmoji.Cross} Invalid sequence of moves. " +
                                             $"Do **{Prefix}rubik help** for commands.");
                            return;
                        }
                    }
                    removeOld = true;
                    break;
            }

            var oldMessage = await cube.GetMessage();
            var newMessage = await ReplyAsync(cube.GetContent(), cube.GetEmbed(Context.Guild));
            cube.MessageId = newMessage.Id;
            cube.ChannelId = Context.Channel.Id;

            if (removeOld && oldMessage != null && oldMessage.Channel.Id == Context.Channel.Id)
            {
                try { await oldMessage.DeleteAsync(DefaultOptions); }
                catch (HttpException) { }
            }
        }




        [Command("pet"), Alias("gotchi", "wakagotchi", "clockagotchi"), Parameters("[command]"), Priority(-4)]
        [Remarks("Adopt your own pet!")]
        [Summary("**__Pet Commands__**\n\n" +
                 "**{prefix}pet** - Check on your pet or adopt if you don't have one\n" +
                 "**{prefix}pet stats [user]** - Check your pet or another's statistics and unlocks\n" +
                 "**{prefix}pet name <name>** - Name your pet\n" +
                 "**{prefix}pet image <image>** - Give your pet an image\n\n" +
                 "**{prefix}pet feed** - Fills your pet's Satiation and restores a little Energy\n" +
                 "**{prefix}pet clean** - Fills your pet's Hygiene\n" +
                 "**{prefix}pet play [user]** - Fills your pet's Happinness. It requires Energy, and consumes a little of every stat. " +
                 "You can make your pet play with another user's pet, in which case they get Happiness for free\n" +
                 "**{prefix}pet sleep/wakeup** - Sleep to restore Energy over time\n\n" +
                 "**{prefix}pet help** - This list of commands\n" +
                 "**{prefix}pet pet** - Pet your pet\n" +
                 "**{prefix}pet user <user>** - See another person's pet\n" +
                 "**{prefix}pet release** - Gives your pet to a loving family that will take care of it (Deletes pet forever)")]
        [BetterRequireBotPermission(ChannelPermission.EmbedLinks | ChannelPermission.AddReactions)]
        public async Task PetMaster(string commandName = "", [Remainder]string args = null)
        {
            var command = typeof(MoreGamesModule).GetMethods()
                .FirstOrDefault(x => x.GetCustomAttribute<PetCommandAttribute>()?.Names.Contains(commandName.ToLower()) ?? false);

            if (command == null)
            {
                await ReplyAsync($"Unknown pet command! Do `{Prefix}pet help` for help");
            }
            else
            {
                var pet = Games.GetForUser<PetGame>(Context.User.Id);
                if (pet == null)
                {
                    if (commandName == "")
                    {
                        pet = new PetGame("", Context.User.Id, Services);
                        Games.Add(pet);
                    }
                    else
                    {
                        await ReplyAsync($"You don't have a pet yet! Simply do `{Prefix}pet` to adopt one.");
                        return;
                    }
                }

                await (Task)command.Invoke(this, new object[] { pet, args });
            }
        }




        [AttributeUsage(AttributeTargets.Method)]
        private class PetCommandAttribute : Attribute
        {
            public string[] Names { get; }
            public PetCommandAttribute(params string[] names)
            {
                Names = names;
            }
        }



        [PetCommand("")]
        public async Task Pet(PetGame pet, string args)
        {
            await ReplyAsync(pet.GetContent(), pet.GetEmbed(Context.User as IGuildUser));
        }


        [PetCommand("exact", "precise", "decimals", "float", "double")]
        public async Task PetExact(PetGame pet, string args)
        {
            var user = Context.User as SocketGuildUser;

            if (!string.IsNullOrWhiteSpace(args))
            {
                var other = await Context.ParseUserAsync(args);
                if (other != null)
                {
                    user = other;
                    pet = Games.GetForUser<PetGame>(user.Id);
                    if (pet == null)
                    {
                        await ReplyAsync("This person doesn't have a pet :(");
                        return;
                    }
                }
            }

            await ReplyAsync(pet.GetContent(), pet.GetEmbed(user, decimals: true));
        }


        [PetCommand("stats", "statistics", "achievements", "unlocks")]
        public async Task PetStats(PetGame pet, string args)
        {
            var user = Context.User as SocketGuildUser;

            if (!string.IsNullOrWhiteSpace(args))
            {
                var other = await Context.ParseUserAsync(args);
                if (other != null)
                {
                    user = other;
                    pet = Games.GetForUser<PetGame>(user.Id);
                    if (pet == null)
                    {
                        await ReplyAsync("This person doesn't have a pet :(");
                        return;
                    }
                }
            }

            await ReplyAsync(pet.GetEmbedAchievements(user));
        }


        [PetCommand("feed", "food", "eat", "hunger", "satiation")]
        public async Task PetFeed(PetGame pet, string args)
        {
            if (pet.TryFeed()) await Context.Message.AddReactionAsync(Bot.Random.Choose(Content.petFoodEmotes).ToEmoji());
            else await ReplyAsync($"{CustomEmoji.Cross} Your pet is already full! (-1 energy)");
        }


        [PetCommand("clean", "hygiene", "wash")]
        public async Task PetClean(PetGame pet, string args)
        {
            if (pet.TryClean()) await Context.Message.AddReactionAsync(Bot.Random.Choose(Content.petCleanEmotes).ToEmoji());
            else await ReplyAsync($"{CustomEmoji.Cross} Your pet is already clean! (-1 energy)");
        }


        [PetCommand("play", "fun", "happy", "happiness")]
        public async Task PetPlay(PetGame pet, string args)
        {
            PetGame otherPet = null;
            if (!string.IsNullOrWhiteSpace(args))
            {
                var otherUser = await Context.ParseUserAsync(args);
                if (otherUser == null)
                {
                    await ReplyAsync("Can't find that user to play with!");
                    return;
                }
                else if ((otherPet = Games.GetForUser<PetGame>(otherUser.Id)) == null)
                {
                    await ReplyAsync("This person doesn't have a pet :(");
                    return;
                }
                else
                {
                    otherPet.UpdateStats();
                    if (otherPet.happiness.Ceiling() == PetGame.MaxStat)
                    {
                        await ReplyAsync("This person's pet doesn't want to play right now!");
                        return;
                    }
                }
            }


            if (pet.TryPlay())
            {
                var playEmote = Bot.Random.Choose(Content.petPlayEmotes).ToEmoji();

                if (otherPet == null) await Context.Message.AddReactionAsync(playEmote);
                else
                {
                    otherPet.happiness = PetGame.MaxStat;
                    Games.Save(otherPet);

                    await ReplyAsync($"{CustomEmoji.PetRight}{playEmote}{CustomEmoji.PetLeft}");
                    await ReplyAsync($"{pet.PetName} and {otherPet.PetName} are happy to play together!");
                }
            }
            else
            {
                string message = pet.happiness.Ceiling() == PetGame.MaxStat
                    ? "Your pet doesn't want to play anymore! (-1 energy)"
                    : "Your pet is too tired! It needs 5 energy, or for someone else's pet to encourage it to play.";

                await ReplyAsync($"{CustomEmoji.Cross} {message}");
            }
        }


        [PetCommand("sleep", "rest", "energy")]
        public async Task PetSleep(PetGame pet, string args)
        {
            pet.UpdateStats(store: false);
            if (pet.energy.Ceiling() == PetGame.MaxStat && !pet.asleep)
            {
                pet.happiness = Math.Max(0, pet.happiness - 1);
                Games.Save(pet);
                await ReplyAsync($"{CustomEmoji.Cross} Your pet is not tired! (-1 happiness)");
            }
            else
            {
                string message = pet.asleep ? "Your pet is already sleeping." : "Your pet is now asleep.";
                await ReplyAsync($"{Bot.Random.Choose(Content.petSleepEmotes)} {message}");
                if (!pet.asleep) pet.ToggleSleep();
            }
        }


        [PetCommand("wake", "wakeup", "awaken")]
        public async Task PetWake(PetGame pet, string args)
        {
            pet.UpdateStats(false);
            await ReplyAsync(pet.asleep ? "🌅 You wake up your pet." : "🌅 Your pet is already awake.");
            if (pet.asleep) pet.ToggleSleep();
        }


        [PetCommand("name")]
        public async Task PetName(PetGame pet, string args)
        {
            if (string.IsNullOrWhiteSpace(args)) await ReplyAsync($"{CustomEmoji.Cross} Please specify a name!");
            else if (args.Length > 32) await ReplyAsync($"{CustomEmoji.Cross} Pet name can't go above 32 characters!");
            else
            {
                pet.PetName = args;
                await AutoReactAsync();
            }
        }


        [PetCommand("image", "url")]
        public async Task PetImage(PetGame pet, string args)
        {
            string url = args ?? Context.Message.Attachments.FirstOrDefault()?.Url;

            if (url == null && pet.PetImageUrl == null)
            {
                await ReplyAsync($"{CustomEmoji.Cross} Please specify an image! You can use a link or upload your own.");
            }
            else
            {
                if (!pet.TrySetImageUrl(url))
                {
                    await ReplyAsync($"{CustomEmoji.Cross} Invalid image link!\nYou could also upload the image yourself.");
                    return;
                }

                if (url == null) await ReplyAsync($"{CustomEmoji.Check} Pet image reset!");
                else await AutoReactAsync();
            }
        }


        [PetCommand("help", "h")]
        public async Task PetHelp(PetGame pet, string args)
        {
            await ReplyAsync(Help.MakeHelp("pet", Prefix));
        }


        [PetCommand("pet", "pat", "pot", "p", "wakagotchi", "gotchi")]
        public async Task PetPet(PetGame pet, string args)
        {
            var now = DateTime.Now;
            var passed = now - pet.petTimerStart;
            if (passed > TimeSpan.FromMinutes(1))
            {
                pet.petTimerStart = now;
                pet.timesPetSinceTimerStart = 0;
            }

            int limit = Context.Guild == null ? 10 : 1;

            if (pet.timesPetSinceTimerStart >= limit)
            {
                if (pet.timesPetSinceTimerStart < limit + 3) // Reattempts
                {
                    pet.timesPetSinceTimerStart += 1;

                    string response = Context.Guild == null
                        ? $"{CustomEmoji.Cross} That's enough petting! {60 - (int)passed.TotalSeconds} seconds left."
                        : $"{CustomEmoji.Cross} You may pet once a minute in guilds. Try DM-ing the bot.";
                        
                    await ReplyAsync(response);
                }
            }
            else
            {
                pet.timesPetSinceTimerStart += 1;
                await ReplyAsync(pet.DoPet());
            }
        }


        [PetCommand("user", "u")]
        public async Task PetUser(PetGame pet, string args)
        {
            if (string.IsNullOrWhiteSpace(args))
            {
                await ReplyAsync("You must specify a user!");
                return;
            }

            var user = await Context.ParseUserAsync(args);

            if (user == null)
            {
                await ReplyAsync("Can't find the specified user!");
                return;
            }

            pet = Games.GetForUser<PetGame>(user.Id);

            if (pet == null) await ReplyAsync("This person doesn't have a pet :(");
            else await ReplyAsync(pet.GetContent(), pet.GetEmbed(user));
        }


        [PetCommand("release")]
        public async Task PetRelease(PetGame pet, string args)
        {
            if (string.IsNullOrWhiteSpace(pet.PetName) || args?.SanitizeMarkdown().SanitizeMentions() == pet.PetName)
            {
                Games.Remove(pet);
                await ReplyAsync($"Goodbye {(string.IsNullOrWhiteSpace(pet.PetName) ? pet.GameName : pet.PetName)}!");
            }
            else
            {
                await ReplyAsync(
                    $"❗ Are you sure you want to delete {pet.PetName}? It will be gone forever, along with your stats and achievements, " +
                    $"and you can't get it back. Do **{Prefix}pet release {pet.PetName}** to release.");
            }
        }
    }
}
