using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Collections.Generic;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using PacManBot.Constants;
using PacManBot.Extensions;
using PacManBot.Games.Concrete;

namespace PacManBot.Commands.Modules
{
    public partial class MoreGamesModule
    {
        [AttributeUsage(AttributeTargets.Method)]
        private class PetCommandAttribute : Attribute
        {
            public string[] Names { get; }
            public PetCommandAttribute(params string[] names)
            {
                Names = names;
            }
        }

        [AttributeUsage(AttributeTargets.Method)]
        private class RequiresPetAttribute : Attribute
        {
        }


        private static readonly IEnumerable<MethodInfo> PetMethods = typeof(MoreGamesModule).GetMethods()
            .Where(x => x.GetCustomAttribute<PetCommandAttribute>() != null)
            .ToList();

        public string AdoptPetMessage => $"You don't have a pet yet! Do `{Prefix}pet adopt` to adopt one.";


        [Command("pet"), Alias("gotchi", "wakagotchi", "clockagotchi"), Parameters("[command]"), Priority(5)]
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
                 "**{prefix}pet adopt [name]** - Adopt a new pet!\n" +
                 "**{prefix}pet release** - Gives your pet to a loving family that will take care of it (Deletes pet forever)")]
        public async Task PetMaster(string commandName = "", [Remainder]string args = null)
        {
            commandName = commandName.ToLower();

            var command = PetMethods
                .FirstOrDefault(x => x.GetCustomAttribute<PetCommandAttribute>().Names.Contains(commandName));

            if (command == null)
            {
                await ReplyAsync($"Unknown pet command! Do `{Prefix}pet help` for help");
            }
            else
            {
                var pet = Games.GetForUser<PetGame>(Context.User.Id);
                if (pet == null && command.GetCustomAttribute<RequiresPetAttribute>() != null)
                {
                    await ReplyAsync(AdoptPetMessage);
                    return;
                }

                await (Task)command.Invoke(this, new object[] { pet, args });
            }
        }




        [PetCommand("")]
        [RequiresPet]
        public async Task PetSendProfile(PetGame pet, string args)
        {
            await ReplyAsync(pet.GetContent(), pet.GetEmbed(Context.User as IGuildUser));
        }


        [PetCommand("exact", "precise", "decimals", "float", "double")]
        public async Task PetSendExact(PetGame pet, string args)
        {
            var user = Context.User as SocketGuildUser;

            if (!string.IsNullOrEmpty(args))
            {
                user = await Context.ParseUserAsync(args);
                if (user == null)
                {
                    await ReplyAsync("Can't find that user!");
                    return;
                }
                else
                {
                    pet = Games.GetForUser<PetGame>(user.Id);
                    if (pet == null)
                    {
                        await ReplyAsync("This person doesn't have a pet :(");
                        return;
                    }
                }
            }

            if (pet == null) await ReplyAsync(AdoptPetMessage);
            else await ReplyAsync(pet.GetContent(), pet.GetEmbed(user, decimals: true));
        }


        [PetCommand("stats", "statistics", "achievements", "unlocks")]
        public async Task PetSendStats(PetGame pet, string args)
        {
            var user = Context.User as SocketGuildUser;

            if (!string.IsNullOrEmpty(args))
            {
                user = await Context.ParseUserAsync(args);
                if (user == null)
                {
                    await ReplyAsync("Can't find that user!");
                    return;
                }
                else
                {
                    pet = Games.GetForUser<PetGame>(user.Id);
                    if (pet == null)
                    {
                        await ReplyAsync("This person doesn't have a pet :(");
                        return;
                    }
                }
            }

            if (pet == null) await ReplyAsync(AdoptPetMessage);
            else await ReplyAsync(pet.GetEmbedAchievements(user));
        }


        [PetCommand("feed", "food", "eat", "hunger", "satiation")]
        [RequiresPet]
        public async Task PetFeed(PetGame pet, string args)
        {
            if (pet.TryFeed()) await Context.Message.AddReactionAsync(Bot.Random.Choose(Content.petFoodEmotes).ToEmoji());
            else await ReplyAsync($"{CustomEmoji.Cross} Your pet is already full! (-1 energy)");
        }


        [PetCommand("clean", "hygiene", "wash")]
        [RequiresPet]
        public async Task PetClean(PetGame pet, string args)
        {
            if (pet.TryClean()) await Context.Message.AddReactionAsync(Bot.Random.Choose(Content.petCleanEmotes).ToEmoji());
            else await ReplyAsync($"{CustomEmoji.Cross} Your pet is already clean! (-1 energy)");
        }


        [PetCommand("play", "fun", "happy", "happiness")]
        [RequiresPet]
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
                    await ReplyAsync($"{pet.petName} and {otherPet.petName} are happy to play together!");
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
        [RequiresPet]
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
        [RequiresPet]
        public async Task PetWake(PetGame pet, string args)
        {
            pet.UpdateStats(false);
            await ReplyAsync(pet.asleep ? "🌅 You wake up your pet." : "🌅 Your pet is already awake.");
            if (pet.asleep) pet.ToggleSleep();
        }


        [PetCommand("name")]
        [RequiresPet]
        public async Task PetName(PetGame pet, string args)
        {
            if (string.IsNullOrWhiteSpace(args)) await ReplyAsync($"{CustomEmoji.Cross} Please specify a name!");
            else if (args.Length > 32) await ReplyAsync($"{CustomEmoji.Cross} Pet name can't go above 32 characters!");
            else if (args.Contains("@")) await ReplyAsync($"{CustomEmoji.Cross} Pet name can't contain \"@\"!");
            else
            {
                pet.SetPetName(args);
                Games.Save(pet);
                await AutoReactAsync();
            }
        }


        [PetCommand("image", "url")]
        [RequiresPet]
        public async Task PetImage(PetGame pet, string args)
        {
            string url = args ?? Context.Message.Attachments.FirstOrDefault()?.Url;

            if (url == null && pet.petImageUrl == null)
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

                Games.Save(pet);

                if (url == null) await ReplyAsync($"{CustomEmoji.Check} Pet image reset!");
                else await AutoReactAsync();
            }
        }


        [PetCommand("help", "h")]
        public async Task PetSendHelp(PetGame pet, string args)
        {
            await ReplyAsync(Help.MakeHelp("pet", Prefix));
        }


        [PetCommand("pet", "pat", "pot", "p", "wakagotchi", "gotchi")]
        [RequiresPet]
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
        public async Task PetSendUser(PetGame pet, string args)
        {
            if (string.IsNullOrWhiteSpace(args))
            {
                await ReplyAsync("You must specify a user!");
                return;
            }

            var user = await Context.ParseUserAsync(args);

            if (user == null)
            {
                await ReplyAsync("Can't find that user!");
                return;
            }

            pet = Games.GetForUser<PetGame>(user.Id);

            if (pet == null) await ReplyAsync("This person doesn't have a pet :(");
            else await ReplyAsync(pet.GetContent(), pet.GetEmbed(user));
        }


        [PetCommand("adopt", "get")]
        public async Task PetAdopt(PetGame pet, string args)
        {
            if (pet != null)
            {
                await ReplyAsync($"You already have a pet!");
            }
            else
            {
                pet = new PetGame(args.Replace("@", "").Truncate(32), Context.User.Id, Services);
                Games.Add(pet);
                await PetSendProfile(pet, null);
            }
        }


        [PetCommand("release")]
        [RequiresPet]
        public async Task PetRelease(PetGame pet, string args)
        {
            if (string.IsNullOrWhiteSpace(pet.petName) || args?.SanitizeMarkdown() == pet.petName)
            {
                Games.Remove(pet);
                await ReplyAsync($"Goodbye {(string.IsNullOrWhiteSpace(pet.petName) ? pet.GameName : pet.petName)}!");
            }
            else
            {
                await ReplyAsync(
                    $"❗ Are you sure you want to delete {pet.petName}? It will be gone forever, along with your stats and achievements, " +
                    $"and you can't get it back. Do **{Prefix}pet release {pet.petName}** to release.");
            }
        }
    }
}
