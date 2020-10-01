using System;
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
    [Description(ModuleNames.Games)]
    public class PetModule : BaseGameModule<PetGame>
    {
        public string AdoptPetMessage(CommandContext ctx)
            => $"You don't have a pet yet! Do `{Storage.GetPrefix(ctx)}pet adopt` to adopt one.";


        [GroupCommand, Priority(-1)]
        [Description("View your pet")]
        public async Task SendProfile(CommandContext ctx)
        {
            var pet = Game(ctx);
            if (pet == null)
            {
                await ctx.RespondAsync(AdoptPetMessage(ctx));
                return;
            }
            await ctx.RespondAsync(await pet.GetContentAsync(), await pet.GetEmbedAsync(ctx.Member));
        }


        [Command("exact"), Aliases("precise", "decimals", "float", "double")]
        [Description("View your pet with precise meters")]
        public async Task SendExact(CommandContext ctx, DiscordMember otherMember = null)
        {
            var member = ctx.Member;
            var pet = Game(ctx);

            if (otherMember != null)
            {
                member = otherMember;
                pet = Games.GetForUser<PetGame>(member.Id);
                if (pet == null)
                {
                    await ctx.RespondAsync("This person doesn't have a pet :(");
                    return;
                }
            }

            if (pet == null)
            {
                await ctx.RespondAsync(AdoptPetMessage(ctx));
                return;
            }

            await ctx.RespondAsync(await pet.GetContentAsync(), await pet.GetEmbedAsync(member, decimals: true));
        }


        [Command("stats"), Aliases("statistics", "achievements", "unlocks")]
        [Description("View your pet's statistics")]
        public async Task SendStats(CommandContext ctx, DiscordMember otherMember = null)
        {
            var member = ctx.Member;
            var pet = Game(ctx);

            if (otherMember != null)
            {
                member = otherMember;
                pet = Games.GetForUser<PetGame>(member.Id);
                if (pet == null)
                {
                    await ctx.RespondAsync("This person doesn't have a pet :(");
                    return;
                }
            }

            if (pet == null)
            {
                await ctx.RespondAsync(AdoptPetMessage(ctx));
                return;
            }

            await ctx.RespondAsync(await pet.GetEmbedAchievementsAsync(member));
        }


        [Command("feed"), Aliases("food", "eat", "hunger", "satiation")]
        [Description("Fills your pet's Satiation and restores a little Energy")]
        public async Task Feed(CommandContext ctx)
        {
            if (Game(ctx) == null)
            {
                await ctx.RespondAsync(AdoptPetMessage(ctx));
                return;
            }

            if (!await Game(ctx).TryFeedAsync())
            {
                await ctx.RespondAsync($"{CustomEmoji.Cross} Your pet is already full! (-1 energy)");
                return;
            }
            await ctx.Message.CreateReactionAsync(Program.Random.Choose(Content.petFoodEmotes).ToEmoji());
        }


        [Command("clean"), Aliases("hygiene", "wash")]
        [Description("Fills your pet's Hygiene")]
        public async Task Clean(CommandContext ctx)
        {
            if (Game(ctx) == null)
            {
                await ctx.RespondAsync(AdoptPetMessage(ctx));
                return;
            }

            if (!await Game(ctx).TryCleanAsync())
            {
                await ctx.RespondAsync($"{CustomEmoji.Cross} Your pet is already clean! (-1 energy)");
                return;
            }
            await ctx.Message.CreateReactionAsync(Program.Random.Choose(Content.petCleanEmotes).ToEmoji());
        }


        [Command("play"), Aliases("fun", "happy", "happiness")]
        [Description("Fills your pet's Happinness. It requires Energy, and consumes a little of every stat. " +
        "You can make your pet play with another user's pet, in which case they get Happiness for free!\n")]
        public async Task Play(CommandContext ctx, DiscordMember otherMember = null)
        {
            if (Game(ctx) == null)
            {
                await ctx.RespondAsync(AdoptPetMessage(ctx));
                return;
            }

            PetGame otherPet = null;
            if (otherMember != null)
            {
                if ((otherPet = Games.GetForUser<PetGame>(otherMember.Id)) == null)
                {
                    await ctx.RespondAsync("This person doesn't have a pet :(");
                    return;
                }
                
                otherPet.UpdateStats();

                if (otherPet.happiness.Ceiling() == PetGame.MaxStat)
                {
                    await ctx.RespondAsync("This person's pet doesn't want to play right now!");
                    return;
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
                    await ctx.RespondAsync($"{Game(ctx).PetName} and {otherPet.PetName} are happy to play together!");
                }
            }
            else
            {
                string message = Game(ctx).happiness.Ceiling() == PetGame.MaxStat
                    ? "Your pet doesn't want to play anymore! (-1 energy)"
                    : "Your pet is too tired! It needs 5 energy, or for someone else's pet to encourage it to play.";

                await ctx.RespondAsync($"{CustomEmoji.Cross} {message}");
            }
        }


        [Command("sleep"), Aliases("rest", "energy")]
        [Description("Sleep to restore Energy over time")]
        public async Task Sleep(CommandContext ctx)
        {
            if (Game(ctx) == null)
            {
                await ctx.RespondAsync(AdoptPetMessage(ctx));
                return;
            }

            Game(ctx).UpdateStats();
            if (Game(ctx).energy.Ceiling() == PetGame.MaxStat && !Game(ctx).asleep)
            {
                Game(ctx).happiness = Math.Max(0, Game(ctx).happiness - 1);
                await SaveGameAsync(ctx);
                await ctx.RespondAsync($"{CustomEmoji.Cross} Your pet is not tired! (-1 happiness)");
            }

            string message = Game(ctx).asleep ? "Your pet is already sleeping." : "Your pet is now asleep.";
            if (!Game(ctx).asleep) await Game(ctx).ToggleSleepAsync();
            await ctx.RespondAsync($"{Program.Random.Choose(Content.petSleepEmotes)} {message}");
        }


        [Command("wake"), Aliases("wakeup", "awaken", "awake")]
        [Description("Wake up your pet")]
        public async Task WakeUp(CommandContext ctx)
        {
            if (Game(ctx) == null)
            {
                await ctx.RespondAsync(AdoptPetMessage(ctx));
                return;
            }

            Game(ctx).UpdateStats();
            var message = Game(ctx).asleep ? "🌅 You wake up your pet." : "🌅 Your pet is already awake.";
            if (Game(ctx).asleep) await Game(ctx).ToggleSleepAsync();
            await ctx.RespondAsync(message);
        }


        [Command("name")]
        public async Task SetName(CommandContext ctx, string arg)
        {
            if (Game(ctx) == null)
            {
                await ctx.RespondAsync(AdoptPetMessage(ctx));
                return;
            }

            var msg = ctx.Message;
            string name = arg;

            if (name == "")
            {
                await ctx.RespondAsync("Say your pet's new name:");

                msg = await ctx.GetResponseAsync();
                if (msg == null)
                {
                    await ctx.RespondAsync("Timed out 💨");
                    return;
                }

                name = msg.Content;
                if (string.IsNullOrWhiteSpace(name))
                {
                    await ctx.RespondAsync(null);
                    return;
                }
            }

            if (name.Length > PetGame.NameCharLimit)
            {
                await ctx.RespondAsync($"{CustomEmoji.Cross} Pet name can't go above 32 characters!");
                return;
            }

            await Game(ctx).SetPetNameAsync(name);
            await msg.AutoReactAsync();
        }


        [Command("image"), Aliases("url", "avatar")]
        [Description("Set your pet's image")]
        public async Task SetImage(CommandContext ctx, string arg)
        {
            if (Game(ctx) == null)
            {
                await ctx.RespondAsync(AdoptPetMessage(ctx));
                return;
            }

            var msg = ctx.Message;
            string url = msg.Attachments.FirstOrDefault()?.Url ?? arg;

            if (string.IsNullOrWhiteSpace(url))
            {
                await ctx.RespondAsync("Send your pet's new image or image URL, or \"reset\" to reset it.");

                msg = await ctx.GetResponseAsync(120);
                if (msg == null)
                {
                    await ctx.RespondAsync("Timed out 💨");
                    return;
                }

                url = msg.Attachments.FirstOrDefault()?.Url ?? msg.Content;
                if (string.IsNullOrWhiteSpace(url))
                {
                    await ctx.RespondAsync(null);
                    return;
                }
            }

            if (url.ToLowerInvariant() == "reset")
            {
                await Game(ctx).TrySetImageUrlAsync(null);
                await ctx.RespondAsync($"{CustomEmoji.Check} Pet image reset!");
            }

            if (!await Game(ctx).TrySetImageUrlAsync(url))
            {
                await ctx.RespondAsync($"{CustomEmoji.Cross} Invalid image!");
                return;
            }

            await msg.AutoReactAsync();
        }


        [Command("pet"), Aliases("pat", "pot", "p", "wakagotchi", "gotchi")]
        [Description("Pet your pet")]
        public async Task PetPet(CommandContext ctx)
        {
            if (Game(ctx) == null)
            {
                await ctx.RespondAsync(AdoptPetMessage(ctx));
                return;
            }

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
            }
            else
            {
                Game(ctx).timesPetSinceTimerStart += 1;
                await ctx.RespondAsync(await Game(ctx).DoPetAsync());
            }
        }


        [Command("abuse"), Hidden, Description("no")]
        public async Task Meme(CommandContext ctx)
        {
            if (Game(ctx) == null)
            {
                await ctx.RespondAsync(AdoptPetMessage(ctx));
                return;
            }

            if (Game(ctx).timesPetSinceTimerStart > 0)
            {
                await ctx.RespondAsync(null);
                return;
            }
            Game(ctx).timesPetSinceTimerStart = 10;
            await ctx.RespondAsync("no");
        }


        [Command("user")]
        [Description("View another person's pet")]
        public async Task SendUser(CommandContext ctx, DiscordMember member)
        {
            var pet = Games.GetForUser<PetGame>(member.Id);
            if (pet == null)
            {
                await ctx.RespondAsync("This person doesn't have a pet :(");
                return;
            }

            await ctx.RespondAsync(await pet.GetContentAsync(), await pet.GetEmbedAsync(member));
        }


        [Command("adopt")]
        [Description("Adopt your new pet!")]
        public async Task StartGame(CommandContext ctx)
        {
            if (Game(ctx) != null)
            {
                await ctx.RespondAsync($"You already have a pet!");
                return;
            }

            StartNewGame(new PetGame(ctx.User.Id, Services));
            await SendProfile(ctx);
            await SaveGameAsync(ctx);
        }


        [Command("release")]
        [Description("Gives your pet to a loving family that will take care of it (Deletes pet forever)")]
        public async Task DeleteGame(CommandContext ctx)
        {
            if (Game(ctx) == null)
            {
                await ctx.RespondAsync(AdoptPetMessage(ctx));
                return;
            }

            if (string.IsNullOrWhiteSpace(Game(ctx).PetName))
            {
                Games.Remove(Game(ctx));
                await ctx.RespondAsync($"Goodbye {Game(ctx).GameName}!");
                return;
            }

            await ctx.RespondAsync(
                $"❗ Are you sure you want to release **{Game(ctx).PetName}**?\n" +
                $"It will be gone forever, along with your stats and achievements, and you can't get it back.\n" +
                $"Release your pet? (Yes/No)");

            if (await ctx.GetYesResponseAsync() ?? false)
            {
                Games.Remove(Game(ctx));
                await ctx.RespondAsync($"Goodbye {Game(ctx).PetName}!");
            }
            else await ctx.RespondAsync("Pet not released ❤");
        }
    }
}
