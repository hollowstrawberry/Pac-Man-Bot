using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using PacManBot.Constants;
using PacManBot.Extensions;
using PacManBot.Games.Concrete;

namespace PacManBot.Commands.Modules
{
    [Group(ModuleNames.Games), Description("3")]
    [SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Reflection")]
    public class PetModule : BaseGameModule<PetGame>
    {
        private static readonly IEnumerable<MethodInfo> PetMethods = typeof(PetModule).GetMethods()
            .Where(x => x.Get<PetCommandAttribute>()?.VerifyMethod(x) != null)
            .ToArray();

        [AttributeUsage(AttributeTargets.Method)]
        private class RequiresPetAttribute : Attribute { }

        [AttributeUsage(AttributeTargets.Method)]
        private class PetCommandAttribute : Attribute
        {
            public string[] Names { get; }
            public PetCommandAttribute(params string[] names)
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




        public string AdoptPetMessage(CommandContext ctx)
            => $"You don't have a pet yet! Do `{Storage.GetPrefix(ctx)}pet adopt` to adopt one.";


        [Command("pet"), Aliases("gotchi", "wakagotchi", "clockagotchi"), Priority(2)]
        [Description(
            "**__Pet Commands__**\n\n" +
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
        public async Task PetMaster(CommandContext ctx, string commandName = "", [RemainingText]string args = "")
        {
            commandName = commandName.ToLowerInvariant();

            var command = PetMethods
                .FirstOrDefault(x => x.GetCustomAttribute<PetCommandAttribute>().Names.Contains(commandName));

            if (command == null)
            {
                await ctx.RespondAsync($"Unknown pet command! Do `{Storage.GetPrefix(ctx)}pet help` for help");
            }
            else
            {
                if (Game(ctx) == null && command.Get<RequiresPetAttribute>() != null)
                {
                    await ctx.RespondAsync(AdoptPetMessage(ctx));
                    return;
                }

                string response = await command.Invoke<Task<string>>(this, ctx, args);
                if (response != null) await ctx.RespondAsync(response);
            }
        }




        [PetCommand(""), RequiresPet]
        public async Task<string> SendProfile(CommandContext ctx, string arg)
        {
            await ctx.RespondAsync(await Game(ctx).GetContentAsync(), await Game(ctx).GetEmbedAsync(ctx.Member));
            return null;
        }


        [PetCommand("exact", "precise", "decimals", "float", "double")]
        public async Task<string> SendExact(CommandContext ctx, string arg)
        {
            var member = ctx.Member;
            var pet = Game(ctx);

            if (arg != "")
            {
                member = (DiscordMember)await ctx.Client.GetCommandsNext().ConvertArgument<DiscordMember>(arg, ctx);
                if (member == null) return "Can't find that user!";

                pet = Games.GetForUser<PetGame>(member.Id);
                if (pet == null) return "This person doesn't have a pet :(";
            }

            if (pet == null) return AdoptPetMessage(ctx);

            await ctx.RespondAsync(await pet.GetContentAsync(), await pet.GetEmbedAsync(member, decimals: true));
            return null;
        }


        [PetCommand("stats", "statistics", "achievements", "unlocks")]
        public async Task<string> SendStats(CommandContext ctx, string arg)
        {
            var member = ctx.Member;
            var pet = Game(ctx);

            if (arg != "")
            {
                member = (DiscordMember)await ctx.Client.GetCommandsNext().ConvertArgument<DiscordMember>(arg, ctx);
                if (member == null) return "Can't find that user!";

                pet = Games.GetForUser<PetGame>(member.Id);
                if (pet == null) return "This person doesn't have a pet :(";
            }

            if (pet == null) return AdoptPetMessage(ctx);

            await ctx.RespondAsync(await pet.GetEmbedAchievementsAsync(member));
            return null;
        }


        [PetCommand("feed", "food", "eat", "hunger", "satiation"), RequiresPet]
        public async Task<string> Feed(CommandContext ctx, string arg)
        {
            if (!await Game(ctx).TryFeedAsync()) return $"{CustomEmoji.Cross} Your pet is already full! (-1 energy)";
            await ctx.Message.CreateReactionAsync(Program.Random.Choose(Content.petFoodEmotes).ToEmoji());
            return null;
        }


        [PetCommand("clean", "hygiene", "wash"), RequiresPet]
        public async Task<string> Clean(CommandContext ctx, string arg)
        {
            if (!await Game(ctx).TryCleanAsync()) return $"{CustomEmoji.Cross} Your pet is already clean! (-1 energy)";
            await ctx.Message.CreateReactionAsync(Program.Random.Choose(Content.petCleanEmotes).ToEmoji());
            return null;
        }


        [PetCommand("play", "fun", "happy", "happiness"), RequiresPet]
        public async Task<string> Play(CommandContext ctx, string arg)
        {
            PetGame otherPet = null;
            if (arg != "")
            {
                var otherUser = (DiscordMember)await ctx.Client.GetCommandsNext().ConvertArgument<DiscordMember>(arg, ctx);
                if (otherUser == null)
                {
                    return "Can't find that user to play with!";
                }
                else if ((otherPet = Games.GetForUser<PetGame>(otherUser.Id)) == null)
                {
                    return "This person doesn't have a pet :(";
                }
                else
                {
                    otherPet.UpdateStats();
                    if (otherPet.happiness.Ceiling() == PetGame.MaxStat) return "This person's pet doesn't want to play right now!";
                }
            }

            if (await Game(ctx).TryPlayAsync())
            {
                var playEmote = Program.Random.Choose(Content.petPlayEmotes).ToEmoji();

                if (otherPet == null) await ctx.Message.CreateReactionAsync(playEmote);
                else
                {
                    otherPet.happiness = PetGame.MaxStat;
                    await Games.SaveAsync(otherPet);

                    await ctx.RespondAsync($"{CustomEmoji.PetRight}{playEmote}{CustomEmoji.PetLeft}");
                    await ctx.RespondAsync($"{Game(ctx).petName} and {otherPet.petName} are happy to play together!");
                }
                return null;
            }
            else
            {
                string message = Game(ctx).happiness.Ceiling() == PetGame.MaxStat
                    ? "Your pet doesn't want to play anymore! (-1 energy)"
                    : "Your pet is too tired! It needs 5 energy, or for someone else's pet to encourage it to play.";

                return $"{CustomEmoji.Cross} {message}";
            }
        }


        [PetCommand("sleep", "rest", "energy"), RequiresPet]
        public async Task<string> Sleep(CommandContext ctx, string arg)
        {
            Game(ctx).UpdateStats();
            if (Game(ctx).energy.Ceiling() == PetGame.MaxStat && !Game(ctx).asleep)
            {
                Game(ctx).happiness = Math.Max(0, Game(ctx).happiness - 1);
                await SaveGameAsync(ctx);
                return $"{CustomEmoji.Cross} Your pet is not tired! (-1 happiness)";
            }

            string message = Game(ctx).asleep ? "Your pet is already sleeping." : "Your pet is now asleep.";
            if (!Game(ctx).asleep) await Game(ctx).ToggleSleepAsync();
            return $"{Program.Random.Choose(Content.petSleepEmotes)} {message}";
        }


        [PetCommand("wake", "wakeup", "awaken", "awake"), RequiresPet]
        public async Task<string> WakeUp(CommandContext ctx, string arg)
        {
            Game(ctx).UpdateStats();
            var message = Game(ctx).asleep ? "🌅 You wake up your pet." : "🌅 Your pet is already awake.";
            if (Game(ctx).asleep) await Game(ctx).ToggleSleepAsync();
            return message;
        }


        [PetCommand("name"), RequiresPet]
        public async Task<string> SetName(CommandContext ctx, string arg)
        {
            var msg = ctx.Message;
            string name = arg;

            if (name == "")
            {
                await ctx.RespondAsync("Say your pet's new name:");

                msg = await ctx.GetResponseAsync();
                if (msg == null) return "Timed out 💨";

                name = msg.Content;
                if (string.IsNullOrWhiteSpace(name)) return null;
            }

            if (name.Length > PetGame.NameCharLimit)
                return $"{CustomEmoji.Cross} Pet name can't go above 32 characters!";


            await Game(ctx).SetPetNameAsync(name);
            await msg.AutoReactAsync();
            return null;
        }


        [PetCommand("image", "url"), RequiresPet]
        public async Task<string> SetImage(CommandContext ctx, string arg)
        {
            var msg = ctx.Message;
            string url = msg.Attachments.FirstOrDefault()?.Url ?? arg;

            if (string.IsNullOrWhiteSpace(url))
            {
                await ctx.RespondAsync("Send your pet's new image or image URL, or \"reset\" to reset it.");

                msg = await ctx.GetResponseAsync(120);
                if (msg == null) return "Timed out 💨";

                url = msg.Attachments.FirstOrDefault()?.Url ?? msg.Content;
                if (string.IsNullOrWhiteSpace(url)) return null;
            }

            if (url.ToLowerInvariant() == "reset")
            {
                await Game(ctx).TrySetImageUrlAsync(null);
                return $"{CustomEmoji.Check} Pet image reset!";
            }

            if (!await Game(ctx).TrySetImageUrlAsync(url)) return $"{CustomEmoji.Cross} Invalid image!";

            await msg.AutoReactAsync();
            return null;
        }


        [PetCommand("help", "h")]
        public async Task<string> SendHelp(CommandContext ctx, string arg)
        {
            var desc = typeof(PetModule).GetMethod(nameof(PetMaster)).GetCustomAttribute<DescriptionAttribute>();
            await ctx.RespondAsync(desc.Description);
            return null;
        }


        [PetCommand("pet", "pat", "pot", "p", "wakagotchi", "gotchi"), RequiresPet]
        public async Task<string> PetPet(CommandContext ctx, string arg)
        {
            var now = DateTime.Now;
            var passed = now - Game(ctx).petTimerStart;
            if (passed > TimeSpan.FromMinutes(1))
            {
                Game(ctx).petTimerStart = now;
                Game(ctx).timesPetSinceTimerStart = 0;
            }

            int limit = ctx.Guild == null ? 10 : 1;

            if (Game(ctx).timesPetSinceTimerStart >= limit)
            {
                if (Game(ctx).timesPetSinceTimerStart < limit + 3) // Reattempts
                {
                    Game(ctx).timesPetSinceTimerStart += 1;

                    string response = ctx.Guild == null
                        ? $"{CustomEmoji.Cross} That's enough petting! {60 - (int)passed.TotalSeconds} seconds left."
                        : $"{CustomEmoji.Cross} You may pet once a minute in guilds. You can pet more in DMs with the bot.";
                        
                    await ctx.RespondAsync(response);
                }
                return null;
            }
            else
            {
                Game(ctx).timesPetSinceTimerStart += 1;
                return await Game(ctx).DoPetAsync();
            }
        }


        [PetCommand("abuse"), RequiresPet]
        public Task<string> Meme(CommandContext ctx, string arg)
        {
            if (Game(ctx).timesPetSinceTimerStart > 0) return null;
            Game(ctx).timesPetSinceTimerStart = 10;
            return Task.FromResult("no");
        }


        [PetCommand("user", "u")]
        public async Task<string> SendUser(CommandContext ctx, string arg)
        {
            if (arg == "") return "You must specify a user!";

            var member = (DiscordMember)await ctx.Client.GetCommandsNext().ConvertArgument<DiscordMember>(arg, ctx);
            if (member == null) return "Can't find that user!";

            var pet = Games.GetForUser<PetGame>(member.Id);
            if (pet == null) return "This person doesn't have a pet :(";

            await ctx.RespondAsync(await pet.GetContentAsync(), await pet.GetEmbedAsync(member));
            return null;
        }


        [PetCommand("adopt", "get")]
        public async Task<string> StartGame(CommandContext ctx, string arg)
        {
            if (Game(ctx) != null) return $"You already have a pet!";

            StartNewGame(new PetGame(ctx.User.Id, Services));
            await SendProfile(ctx, arg);
            await SaveGameAsync(ctx);
            return null;
        }


        [PetCommand("release"), RequiresPet]
        public async Task<string> DeleteGame(CommandContext ctx, string arg)
        {
            if (string.IsNullOrWhiteSpace(Game(ctx).petName))
            {
                Games.Remove(Game(ctx));
                return $"Goodbye {Game(ctx).GameName}!";
            }

            await ctx.RespondAsync(
                $"❗ Are you sure you want to release **{Game(ctx).petName}**?\n" +
                $"It will be gone forever, along with your stats and achievements, and you can't get it back.\n" +
                $"Release your pet? (Yes/No)");

            if (await ctx.GetYesResponseAsync() ?? false)
            {
                Games.Remove(Game(ctx));
                return $"Goodbye {Game(ctx).petName}!";
            }
            return "Pet not released ❤";
        }
    }
}
