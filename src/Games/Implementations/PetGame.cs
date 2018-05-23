using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;
using Discord;
using Discord.WebSocket;
using PacManBot.Constants;
using PacManBot.Services;

namespace PacManBot.Games
{
    [DataContract]
    class PetGame : BaseGame, IStoreableGame
    {
        public static string[] FoodEmotes = new string[] { "🍌", "🍎", "🍊", "🍕", "🌮", "🍩", "🍪", "🍐", "🍉", "🍇", "🍑", "🍧", "🍫", "🥕", "🍼" };
        public static string[] PlayEmotes = new string[] { "⚽", "🏀", "🏈", "🎾", "🏓", "🎨", "🎤", "🎭", "🏐", "🎣", };
        public static string[] CleanEmotes = new string[] { "💧", "🚿", "🛁", "🚽", "🚰", "💦", "👣", "💩" };
        public static string[] BannerUrl = new string[] { null, "https://cdn.discordapp.com/attachments/412314001414815751/448939830433415189/copperbanner.png", "https://cdn.discordapp.com/attachments/412314001414815751/448939834354958370/silverbanner.png", "https://cdn.discordapp.com/attachments/412314001414815751/448939832102617090/goldbanner.png" };

        public const int MaxStat = 20;

        public override string Name => "Clockagotchi";
        public override TimeSpan Expiry => TimeSpan.MaxValue;
        public string GameFile => $"games/pet{OwnerId}.json";
        public int TotalStats => satiation + happiness + hygiene + energy;

        [DataMember] private string petName = null;
        [DataMember] private string petImageUrl = null;
        [DataMember] private int satiation = 15;
        [DataMember] private int happiness = 15;
        [DataMember] private int hygiene = 15;
        [DataMember] private int energy = 15;
        [DataMember] private bool asleep = false;
        [DataMember] private DateTime bornDate;
        [DataMember] private DateTime lastUpdated;
        [DataMember] private Achievements achievements = new Achievements();

        [DataMember] private ulong OwnerId { get { return UserId[0]; } set { UserId = new ulong[] { value }; } }


        public int Satiation => satiation;
        public int Happiness => happiness;
        public int Hygiene => hygiene;
        public int Energy => energy;
        public bool Asleep => asleep;

        public string PetName
        {
            get { return petName; }
            set
            {
                petName = value?.SanitizeMarkdown().SanitizeMentions().Trim('<', '>');
                UpdateStats();
            }
        }

        public string PetImageUrl
        {
            get { return petImageUrl; }
            set
            {
                string url = value?.Trim('<', '>');
                if (url == null || Uri.TryCreate(url, UriKind.Absolute, out _)) petImageUrl = url;
                else throw new InvalidOperationException();
                UpdateStats();
            }
        }




        [DataContract]
        private class Achievements
        {
            [DataMember] public uint timesFed = 0;
            [DataMember] public uint timesPlayed = 0;
            [DataMember] public uint timesCleaned = 0;
            [DataMember] public DateTime lastNeglected = default;

            [DataMember] public uint Attention = 0;
            [DataMember] public bool Custom = false;

            public uint TotalActions => timesFed + timesPlayed + timesCleaned;
            public bool Care1 => TotalActions >= 20;
            public bool Care2 => TotalActions >= 100;
            public bool Care3 => TotalActions >= 500;


            public Achievements() { }


            public void Checks(PetGame pet)
            {
                if (lastNeglected == default) lastNeglected = pet.bornDate; //old pets
                else if (pet.TotalStats == 0) lastNeglected = DateTime.Now;
                var days = (DateTime.Now - lastNeglected).TotalDays;

                if (!string.IsNullOrWhiteSpace(pet.PetName) && !string.IsNullOrWhiteSpace(pet.PetImageUrl)) Custom = true;

                if (days >= 14 && Attention < 3) Attention = 3;
                else if (days >= 7 && Attention < 2) Attention = 2;
                else if (days >= 2 && Attention < 1) Attention = 1;
            }
        }




        private PetGame() { } // Used in serialization

        public PetGame(string name, ulong ownerId, DiscordShardedClient client, LoggingService logger, StorageService storage)
            : base(1, new ulong[] { ownerId }, client, logger, storage)
        {
            petName = name;
            bornDate = DateTime.Now;
            lastUpdated = DateTime.Now;
        }




        public override EmbedBuilder GetEmbed(bool showHelp = true) => GetEmbed(null);
        public EmbedBuilder GetEmbed(IGuildUser owner)
        {
            bool wasAsleep = asleep;
            UpdateStats();

            var description = new StringBuilder();

            string prefix = storage.GetPrefixOrEmpty(owner?.Guild);
            if (string.IsNullOrWhiteSpace(petName)) description.Append($"Congratulations on your new Clockagotchi!\nUse **{prefix}pet name** to name it and **{prefix}pet help** for more info\n\n");

            description.Append($"**Name:** {(string.IsNullOrWhiteSpace(petName) ? "*Unnamed*" : PetName)}\n");

            string age = (DateTime.Now - bornDate).Humanized();
            description.Append($"**Age:** {(age == "Just now" ? "Newborn" : age)}\nᅠ\n");

            if (TotalStats == 0) description.Append("Oh no! Your pet is **Neglected**.\nHurry and make it feel better!\nᅠ");
            else if (TotalStats <= 5) description.Append("😱 Hurry! Your pet doesn't look very well!\nᅠ");


            var status = new StringBuilder();
            if (asleep) status.Append("💤💤💤\n\n");
            else if (wasAsleep) status.Append("Your pet woke up!\n\n");
            status.Append((satiation >= 5 ? "🍎" : "🍽") + $" `Satiation:` {satiation}/{MaxStat}\n");
            status.Append((happiness >= 5 ? "🏈" : "🕸") + $" `Happiness:` {happiness}/{MaxStat}\n");
            status.Append((hygiene >= 5 ? "🛁" : "💩")   + $" `Hygiene  :` {hygiene}/{MaxStat}\n");
            status.Append((energy >= 5 ? "⚡" : "🕳") + $" `Energy   :` {energy}/{MaxStat}\n");


            return new EmbedBuilder
            {
                Title = $"{owner?.Nickname ?? client.GetUser(OwnerId).Username}'s Clockagotchi",
                Description = description.ToString(),
                Color = TotalStats >= 60 ? new Color(0, 200, 0) : TotalStats >= 25 ? new Color(255, 200, 0) : new Color(255, 0, 0),
                ThumbnailUrl = petImageUrl ?? "https://cdn.discordapp.com/attachments/353729197824278541/447979173554946051/clockagotchi.png",
                ImageUrl = BannerUrl[achievements.Attention],
                Fields = new List<EmbedFieldBuilder>
                {
                    new EmbedFieldBuilder()
                    {
                        IsInline = true,
                        Name = "Status",
                        Value = status.ToString(),
                    },
                    new EmbedFieldBuilder()
                    {
                        IsInline = true,
                        Name = "Medals",
                        Value = GetMedals(),
                    },
                }
            };
        }


        public EmbedBuilder GetEmbedAchievements(IGuildUser owner)
        {
            UpdateStats();

            var stats = new StringBuilder();
            stats.Append($"**Times fed:** {achievements.timesFed}\n");
            stats.Append($"**Times played:** {achievements.timesPlayed}\n");
            stats.Append($"**Times cleaned:** {achievements.timesCleaned}\n");
            stats.Append($"**Total actions:** {achievements.TotalActions}\n");
            stats.Append($"**Time without neglect:** {(DateTime.Now - achievements.lastNeglected).Humanized()}\n");
            stats.Append($"*(Neglect occurs when all stats reach 0)*\nᅠ");

            string[] achievs = new string[] { "", "" }; // off, on
            achievs[achievements.Custom ? 1 : 0] += "\n🎖 Give your pet a name and image";
            achievs[achievements.Care1 ? 1 : 0] += "\n🥉 - 20 Total actions";
            achievs[achievements.Care2 ? 1 : 0] += "\n🥈 - 100 Total actions";
            achievs[achievements.Care3 ? 1 : 0] += "\n🥇 - 500 Total actions";
            achievs[achievements.Attention >= 1 ? 1 : 0] += "\n**Copper Banner** - 2 days without neglect";
            achievs[achievements.Attention >= 2 ? 1 : 0] += "\n**Silver Banner** - 7 days without neglect";
            achievs[achievements.Attention >= 3 ? 1 : 0] += "\n**Gold Banner** - 14 days without neglect";

            return new EmbedBuilder
            {
                Title = $"{owner?.Nickname ?? client.GetUser(OwnerId).Username}'s Clockagotchi",
                Color = new Color(150, 0, 220),
                ThumbnailUrl = petImageUrl ?? "https://cdn.discordapp.com/attachments/353729197824278541/447979173554946051/clockagotchi.png",
                Fields = new List<EmbedFieldBuilder>
                {
                    new EmbedFieldBuilder()
                    {
                        IsInline = false,
                        Name = "Statistics 📊",
                        Value = stats.ToString(),
                    },
                    new EmbedFieldBuilder()
                    {
                        IsInline = false,
                        Name = "Achievements 🏆",
                        Value = achievs[1].Replace("\n", $"\n{CustomEmoji.Check}") + achievs[0],
                    },
                }
            };
        }




        public bool UpdateStats(bool store = true)
        {
            var now = DateTime.Now;
            double cycles = (now - lastUpdated).TotalHours;
            int oldSatiation = satiation, oldHappiness = happiness, oldHygiene = hygiene, oldEnergy = energy;


            double asleepMult = 4 * Bot.Random.NextDouble(0.75, 1.25);
            double awakeMult = 1.2 * Bot.Random.NextDouble(0.75, 1.25);
            if (asleep)
            {
                if (energy + (int)(cycles * asleepMult) > MaxStat)
                {
                    asleep = false;
                    cycles -= (MaxStat - energy) / asleepMult;
                    energy = MaxStat - (int)(cycles * awakeMult);
                }
                else
                {
                    energy = Math.Min(MaxStat, energy + (int)(cycles * asleepMult));
                    if (energy == MaxStat) asleep = false;
                }
            }
            else
            {
                energy = Math.Max(0, energy - (int)(cycles * awakeMult));
            }

            satiation = Math.Max(0, satiation - (int)(cycles * Bot.Random.NextDouble(0.75, 1.25)));
            happiness = Math.Max(0, happiness - (int)(cycles * 0.9 * Bot.Random.NextDouble(0.75, 1.25)));
            hygiene = Math.Max(0, hygiene - (int)(cycles * Bot.Random.NextDouble(0.75, 1.25)));


            achievements.Checks(this);

            bool updated = oldSatiation != satiation || oldHappiness != happiness || oldHygiene != hygiene || oldEnergy != energy;
            if (updated) lastUpdated = now;

            if (store) storage.StoreGame(this);

            return updated;
        }


        public bool Feed()
        {
            UpdateStats(false);

            bool canEat = satiation != MaxStat;
            if (canEat)
            {
                satiation = MaxStat;
                energy = Math.Min(MaxStat, energy + 1);
                achievements.timesFed++;
            }
            storage.StoreGame(this);
            return canEat;
        }

        public bool Play()
        {
            UpdateStats(false);

            bool canPlay = happiness != MaxStat && energy >= 5;
            if (canPlay )
            {
                happiness = MaxStat;
                energy = Math.Max(0, energy - 5);
                achievements.timesPlayed++;
            }
            storage.StoreGame(this);
            return canPlay;
        }

        public bool Clean()
        {
            UpdateStats(false);

            bool canClean = hygiene != MaxStat;
            if (canClean)
            {
                hygiene = MaxStat;
                achievements.timesCleaned++;
            }
            storage.StoreGame(this);
            return canClean;
        }

        public void ToggleSleep()
        {
            UpdateStats(false);
            asleep = !asleep;
            storage.StoreGame(this);
        }


        public string GetMedals()
        {
            string medals = "";
            if (achievements.Care3) medals += "🥇 ";
            if (achievements.Care2) medals += "🥈 ";
            if (achievements.Care1) medals += "🥉 ";
            if (achievements.Custom) medals += "🎖";

            return string.IsNullOrWhiteSpace(medals) ? "*None*" : medals;
        }


        public virtual void SetServices(DiscordShardedClient client, LoggingService logger, StorageService storage)
        {
            this.client = client;
            this.logger = logger;
            this.storage = storage;
        }
    }
}
