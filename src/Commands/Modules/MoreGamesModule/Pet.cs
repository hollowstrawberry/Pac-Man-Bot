using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
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


            private static readonly Type ReturnType = typeof(Task<string>);
            private static readonly IEnumerable<Type> ParameterTypes = new[] { typeof(PetGame), typeof(string) };

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
        private class RequiresPetAttribute : Attribute
        {
        }


        private static readonly IEnumerable<MethodInfo> PetMethods = typeof(MoreGamesModule).GetMethods()
            .Where(x => x.Get<PetCommandAttribute>()?.VerifyMethod(x) != null)
            .ToArray();

        public string AdoptPetMessage => $"You don't have a pet yet! Do `{Context.Prefix}pet adopt` to adopt one.";


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
        public async Task PetMaster(string commandName = "", [Remainder]string args = "")
        {
            commandName = commandName.ToLower();

            var command = PetMethods
                .FirstOrDefault(x => x.GetCustomAttribute<PetCommandAttribute>().Names.Contains(commandName));

            if (command == null)
            {
                await ReplyAsync($"Unknown pet command! Do `{Context.Prefix}pet help` for help");
            }
            else
            {
                var pet = Games.GetForUser<PetGame>(Context.User.Id);
                if (pet == null && command.Get<RequiresPetAttribute>() != null)
                {
                    await ReplyAsync(AdoptPetMessage);
                    return;
                }

                string response = await command.Invoke<Task<string>>(this, pet, args);
                if (response != null) await ReplyAsync(response);
            }
        }




        [PetCommand(""), RequiresPet]
        public async Task<string> PetSendProfile(PetGame pet, string args)
        {
            await ReplyAsync(pet.GetContent(), pet.GetEmbed(Context.User as IGuildUser));
            return null;
        }


        [PetCommand("exact", "precise", "decimals", "float", "double")]
        public async Task<string> PetSendExact(PetGame pet, string args)
        {
            var user = Context.User as SocketGuildUser;

            if (args != "")
            {
                user = await Context.ParseUserAsync(args);
                if (user == null) return "Can't find that user!";

                pet = Games.GetForUser<PetGame>(user.Id);
                if (pet == null) return "This person doesn't have a pet :(";
            }

            if (pet == null) return AdoptPetMessage;

            await ReplyAsync(pet.GetContent(), pet.GetEmbed(user, decimals: true));
            return null;
        }


        [PetCommand("stats", "statistics", "achievements", "unlocks")]
        public async Task<string> PetSendStats(PetGame pet, string args)
        {
            var user = Context.User as SocketGuildUser;

            if (args != "")
            {
                user = await Context.ParseUserAsync(args);
                if (user == null) return "Can't find that user!";

                pet = Games.GetForUser<PetGame>(user.Id);
                if (pet == null) return "This person doesn't have a pet :(";
            }

            if (pet == null) return AdoptPetMessage;

            await ReplyAsync(pet.GetEmbedAchievements(user));
            return null;
        }


        [PetCommand("feed", "food", "eat", "hunger", "satiation"), RequiresPet]
        public async Task<string> PetFeed(PetGame pet, string args)
        {
            if (!pet.TryFeed()) return $"{CustomEmoji.Cross} Your pet is already full! (-1 energy)";
            await Context.Message.AddReactionAsync(Program.Random.Choose(Content.petFoodEmotes).ToEmoji());
            return null;
        }


        [PetCommand("clean", "hygiene", "wash"), RequiresPet]
        public async Task<string> PetClean(PetGame pet, string args)
        {
            if (!pet.TryClean()) return $"{CustomEmoji.Cross} Your pet is already clean! (-1 energy)";
            await Context.Message.AddReactionAsync(Program.Random.Choose(Content.petCleanEmotes).ToEmoji());
            return null;
        }


        [PetCommand("play", "fun", "happy", "happiness"), RequiresPet]
        public async Task<string> PetPlay(PetGame pet, string args)
        {
            PetGame otherPet = null;
            if (args != "")
            {
                var otherUser = await Context.ParseUserAsync(args);
                if (otherUser == null) return "Can't find that user to play with!";
                else if ((otherPet = Games.GetForUser<PetGame>(otherUser.Id)) == null) return "This person doesn't have a pet :(";
                else
                {
                    otherPet.UpdateStats();
                    if (otherPet.happiness.Ceiling() == PetGame.MaxStat) return "This person's pet doesn't want to play right now!";
                }
            }

            if (pet.TryPlay())
            {
                var playEmote = Program.Random.Choose(Content.petPlayEmotes).ToEmoji();

                if (otherPet == null) await Context.Message.AddReactionAsync(playEmote);
                else
                {
                    otherPet.happiness = PetGame.MaxStat;
                    Games.Save(otherPet);

                    await ReplyAsync($"{CustomEmoji.PetRight}{playEmote}{CustomEmoji.PetLeft}");
                    await ReplyAsync($"{pet.petName} and {otherPet.petName} are happy to play together!");
                }
                return null;
            }
            else
            {
                string message = pet.happiness.Ceiling() == PetGame.MaxStat
                    ? "Your pet doesn't want to play anymore! (-1 energy)"
                    : "Your pet is too tired! It needs 5 energy, or for someone else's pet to encourage it to play.";

                return $"{CustomEmoji.Cross} {message}";
            }
        }


        [PetCommand("sleep", "rest", "energy"), RequiresPet]
        public Task<string> PetSleep(PetGame pet, string args)
        {
            pet.UpdateStats(store: false);
            if (pet.energy.Ceiling() == PetGame.MaxStat && !pet.asleep)
            {
                pet.happiness = Math.Max(0, pet.happiness - 1);
                Games.Save(pet);
                return Task.FromResult($"{CustomEmoji.Cross} Your pet is not tired! (-1 happiness)");
            }

            string message = pet.asleep ? "Your pet is already sleeping." : "Your pet is now asleep.";
            if (!pet.asleep) pet.ToggleSleep();
            return Task.FromResult($"{Program.Random.Choose(Content.petSleepEmotes)} {message}");
        }


        [PetCommand("wake", "wakeup", "awaken", "awake"), RequiresPet]
        public Task<string> PetWake(PetGame pet, string args)
        {
            pet.UpdateStats(false);
            var message = pet.asleep ? "🌅 You wake up your pet." : "🌅 Your pet is already awake.";
            if (pet.asleep) pet.ToggleSleep();
            return Task.FromResult(message);
        }


        [PetCommand("name"), RequiresPet]
        public async Task<string> PetName(PetGame pet, string args)
        {
            if (args == "") return $"{CustomEmoji.Cross} Please specify a name!";
            if (args.Length > 32) return $"{CustomEmoji.Cross} Pet name can't go above 32 characters!";
            if (args.Contains("@")) return $"{CustomEmoji.Cross} Pet name can't contain \"@\"!";

            pet.SetPetName(args);
            Games.Save(pet);
            await AutoReactAsync();
            return null;
        }


        [PetCommand("image", "url"), RequiresPet]
        public async Task<string> PetImage(PetGame pet, string args)
        {
            string url = args != "" ? args : Context.Message.Attachments.FirstOrDefault()?.Url;

            if (url == null && pet.petImageUrl == null)
                return $"{CustomEmoji.Cross} Please specify an image! You can use a link or upload your own.";

            if (!pet.TrySetImageUrl(url))
                return $"{CustomEmoji.Cross} Invalid image link!\nYou could also upload the image yourself.";

            Games.Save(pet);

            if (url == null) return $"{CustomEmoji.Check} Pet image reset!";
            await AutoReactAsync();
            return null;
        }


        [PetCommand("help", "h")]
        public async Task<string> PetSendHelp(PetGame pet, string args)
        {
            await ReplyAsync(Commands.GetCommandHelp("pet", Context.Prefix));
            return null;
        }


        [PetCommand("pet", "pat", "pot", "p", "wakagotchi", "gotchi"), RequiresPet]
        public async Task<string> PetPet(PetGame pet, string args)
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
                        : $"{CustomEmoji.Cross} You may pet once a minute in guilds. You can pet more in DMs with the bot.";
                        
                    await ReplyAsync(response);
                }
                return null;
            }
            else
            {
                pet.timesPetSinceTimerStart += 1;
                return pet.DoPet();
            }
        }


        [PetCommand("user", "u")]
        public async Task<string> PetSendUser(PetGame pet, string args)
        {
            if (args == "") return "You must specify a user!";

            var user = await Context.ParseUserAsync(args);
            if (user == null) return "Can't find that user!";

            pet = Games.GetForUser<PetGame>(user.Id);
            if (pet == null) return "This person doesn't have a pet :(";

            await ReplyAsync(pet.GetContent(), pet.GetEmbed(user));
            return null;
        }


        [PetCommand("adopt", "get")]
        public async Task<string> PetAdopt(PetGame pet, string args)
        {
            if (pet != null) return $"You already have a pet!";

            pet = new PetGame(args.Replace("@", "").Truncate(32), Context.User.Id, Services);
            Games.Add(pet);
            await PetSendProfile(pet, null);
            return null;
        }


        [PetCommand("release"), RequiresPet]
        public async Task<string> PetRelease(PetGame pet, string args)
        {
            if (string.IsNullOrWhiteSpace(pet.petName) || args.SanitizeMarkdown() == pet.petName)
            {
                Games.Remove(pet);
                await ReplyAsync($"Goodbye {(string.IsNullOrWhiteSpace(pet.petName) ? pet.GameName : pet.petName)}!");
                return null;
            }

            return $"❗ Are you sure you want to delete {pet.petName}? It will be gone forever, " +
                   $"along with your stats and achievements, and you can't get it back. " +
                   $"Do **{Context.Prefix}pet release {pet.petName}** to release.";
        }
    }
}
