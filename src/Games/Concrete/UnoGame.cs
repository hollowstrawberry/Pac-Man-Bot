using System;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Discord;
using Discord.Net;
using Discord.WebSocket;
using PacManBot.Utils;
using PacManBot.Services;
using PacManBot.Constants;
using PacManBot.Extensions;

namespace PacManBot.Games
{
    [DataContract]
    public class UnoGame : MultiplayerGame, IMessagesGame, IStoreableGame
    {
        // Constants

        public override string Name => "Uno";
        public override TimeSpan Expiry => TimeSpan.FromDays(2);
        public string FilenameKey => "uno";

        private const int CardsPerPlayer = 7;

        private static readonly Card Wild = new Card(CardType.Wild, CardColor.Black);
        private static readonly Card WildDrawFour = new Card(CardType.WildDrawFour, CardColor.Black);

        private static readonly Color[] RgbCardColor = {
            Colors.Red, Colors.Blue, Colors.Green, Colors.Yellow, Colors.DarkBlack,
        };
        private static readonly string[] CardColorEmote = {
            CustomEmoji.RedSquare, CustomEmoji.BlueSquare, CustomEmoji.GreenSquare, CustomEmoji.YellowSquare, CustomEmoji.BlackSquare,
        };
        private static readonly string[] CardTypeEmote = CustomEmoji.NumberCircle.Concatenate(
            CustomEmoji.UnoSkip, CustomEmoji.UnoReverse, CustomEmoji.AddTwo, CustomEmoji.UnoWild, CustomEmoji.AddFour
        );


        // Fields

        [DataMember] private List<UnoPlayer> players = new List<UnoPlayer>();
        [DataMember] private List<Card> drawPile = new List<Card>();
        [DataMember] private List<Card> discardPile = new List<Card>();
        [DataMember] private bool reversed;
        [IgnoreDataMember] private List<UnoPlayer> updatedPlayers = new List<UnoPlayer>();


        // Properties

        [DataMember] public override State State { get; set; }
        [DataMember] public override DateTime LastPlayed { get; set; }
        [DataMember] public override int Time { get; set; }
        [DataMember] public override ulong ChannelId { get; set; }
        [DataMember] public override ulong MessageId { get; set; } = 1;
        [DataMember] public override Player Turn { get; protected set; }
        [DataMember] public override Player Winner { get; protected set; }

        private Card TopCard { get => discardPile[0]; set => discardPile[0] = value; }
        private UnoPlayer CurrentPlayer => players[(int)Turn];
        private UnoPlayer PlayerAt(Player turn) => players[(int)turn];
        private Player FollowingTurn => reversed ? PreviousPlayer() : NextPlayer();
        private Player PrecedingTurn => reversed ? NextPlayer() : PreviousPlayer();

        public override bool BotTurn => players.Count > 1 && base.BotTurn;
        public override bool AllBots => players.All(x => x.User?.IsBot ?? false);
        public override ulong[] UserId
        {
            get => players.Select(x => x.id).ToArray();
            set => throw new InvalidOperationException("Use AddPlayer and RemovePlayer instead");
        }




        // Types

        private enum UnoState
        {
            None,
            Said,
            Forgot,
            NotCalledOut,
        }


        private enum CardColor
        {
            Red,
            Blue,
            Green,
            Yellow,
            Black,
        }


        private enum CardType
        {
            Zero,
            One,
            Two,
            Three,
            Four,
            Five,
            Six,
            Seven,
            Eight,
            Nine,
            Skip,
            Reverse,
            DrawTwo,
            Wild,
            WildDrawFour,
        }



        [DataContract]
        private class UnoPlayer
        {
            [DataMember] public readonly ulong id;
            [DataMember] public List<Card> cards = new List<Card>();
            [DataMember] public UnoState uno = UnoState.None;

            public DiscordShardedClient client;
            public IUserMessage message;
            private IUser user;

            public IUser User => user ?? (user = client.GetUser(id));


            private UnoPlayer() { } // Used in serialization

            public UnoPlayer(ulong id, DiscordShardedClient client)
            {
                this.id = id;
                this.client = client;
                user = client.GetUser(id);
            }
        }


        [DataContract]
        private struct Card : IComparable<Card>
        {
            [DataMember] public CardType Type { get; private set; }
            [DataMember] public CardColor Color { get; private set; }

            public bool WildType => IsWild(Type);
            public int CardsToDraw => Type == CardType.WildDrawFour ? 4 : Type == CardType.DrawTwo ? 2 : 0;


            public Card(CardType type, CardColor color)
            {
                Type = type;
                Color = color;
            }


            public Card NormalizeColor()
            {
                return WildType && Color != CardColor.Black ? new Card(Type, CardColor.Black) : this;
            }

            public override string ToString()
            {
                string typeStr = Type > CardType.Nine ? $"{Type}" : $"{(int)Type}";
                string colorStr = Color == CardColor.Black ? "" : Color.ToString();

                return WildType ? $"{typeStr} {colorStr}" : colorStr + typeStr;
            }

            public string ToStringBig()
            {
                var sb = new StringBuilder();
                for (int y = 0; y < 5; y++)
                {
                    sb.Append($"{CustomEmoji.Empty}".Multiply(2));
                    for (int x = 0; x < 3; x++)
                    {
                        if (x == 1 && y == 2) sb.Append(CardTypeEmote[(int)Type]);
                        else sb.Append(CardColorEmote[(int)Color]);
                    }
                    sb.Append('\n');
                }
                sb.Append($"{CustomEmoji.Empty}".Multiply(2));
                sb.Append(ToString());

                return sb.ToString();
            }

            public int CompareTo(Card other)
            {
                int result = ((int)Color).CompareTo((int)other.Color); // Sorts by color ascending
                if (result == 0) result = ((int)other.Type).CompareTo(((int)Type)); // Then by type descending
                return result;
            }


            public static bool IsWild(CardType type) => type == CardType.Wild || type == CardType.WildDrawFour;

            public static Card? Parse(string value, UnoGame game)
            {
                bool auto = value.Contains("auto");
                value = value.ReplaceMany(ParseReplacements);

                CardColor? color = EnumTraits<CardColor>.Values
                    .FirstOrNull(x => value.EndsOrStartsWith(x.ToString().ToLower()));
                CardType? type = EnumTraits<CardType>.Values.ToList().Reversed()
                    .FirstOrNull(x => value.EndsOrStartsWith(x.ToString().ToLower()) || x <= CardType.Nine && value.EndsOrStartsWith(((int)x).ToString()));

                if (auto)
                {
                    var cards = game.CurrentPlayer.cards.ToList().Sorted();

                    if (color == null && type == null) return value == "" ? cards.FirstOrNull(game.CanDiscard) : null;
                    if (color == null) return cards.FirstOrNull(x => x.Type == type && game.CanDiscard(x));
                    if (type == null) return cards.FirstOrNull(x => x.Color == color && game.CanDiscard(x));
                }

                if (color == null && type.HasValue && IsWild(type.Value)) color = CardColor.Black;

                if (type.HasValue && color.HasValue) return new Card(type.Value, color.Value);
                else return null;
            }

            private static readonly IReadOnlyDictionary<string, string> ParseReplacements = new Dictionary<string, string>{
                { " ", "" }, { "auto", "" }, { "uno", "" }, { "draw2", "drawtwo" }, { "draw4", "drawfour" },
            }.AsReadOnly();
        }




        // Game methods


        public override void Create(ulong channelId, ulong[] playerIds, DiscordShardedClient client, LoggingService logger, StorageService storage)
        {
            base.Create(channelId, null, client, logger, storage);

            // Make deck
            for (int color = 0; color < 4; color++)
            {
                for (int type = 0; type < 15; type++)
                {
                    var card = new Card((CardType)type, type < 13 ? (CardColor)color : CardColor.Black);
                    drawPile.Add(card);
                    if (type > 0 && type < 13) drawPile.Add(card); // Second batch of colors excluding zero
                }
            }

            Bot.Random.Shuffle(drawPile);
            discardPile = new List<Card> { drawPile.Pop() };

            while (TopCard.Type == CardType.WildDrawFour) // Invalid first cards
            {
                drawPile.Add(discardPile.Pop());
                drawPile.Swap(drawPile.Count - 1, Bot.Random.Next(drawPile.Count - 1));
                discardPile.Add(drawPile.Pop());
            }


            playerIds = playerIds.Distinct().Take(10).ToArray();
            var toInviteIds = playerIds.Skip(1).Where(x => !client.GetUser(x).IsBot);
            var toAddIds = playerIds.Where(x => !toInviteIds.Contains(x));

            if (toInviteIds.Count() > 0)
            {
                var mentions = toInviteIds.Select(x => client.GetUser(x)?.Mention);
                string inviteMsg = $"{string.Join(", ", mentions)} You've been invited to play Uno. " +
                                   $"Type `{storage.GetPrefix(Guild)}uno join` to join.\n";
                Message = inviteMsg;
            }

            foreach (ulong id in toAddIds) AddPlayer(id);

            if (players.Count > 1)
            {
                Message += $"• First card is {TopCard}\n";
                ApplyCardEffect();
            }

            storage.StoreGame(this);
        }



        public bool IsInput(string value, ulong userId)
        {
            if (players.Count < 2) return false;

            value = StripPrefix(value.ToLower());

            if (UserId.Contains(userId))
            {
                if (value == "cards") return true;
                else if (value == "uno" || value == "callout") return players.Any(x => x.uno == UnoState.Forgot);

                if (userId == CurrentPlayer.User?.Id)
                {
                    if (Time > 0 && TopCard.Color == CardColor.Black) return Enum.TryParse<CardColor>(value, true, out _); // Wild color
                    else return value == "draw" || value == "skip" || value.Contains("auto") || Card.Parse(value, this).HasValue;
                }
            }

            return false;
        }


        public void Input(string input, ulong userId = 1)
        {
            LastPlayed = DateTime.Now;
            input = StripPrefix(input.ToLower());
            bool calledByAi = CurrentPlayer.User.IsBot;


            // Send cards
            if (input == "cards")
            {
                SendCards(players.First(x => x.User?.Id == userId));
                return;
            }

            // Saying "uno" mechanics (non-bots)
            else if (input == "uno" || input == "callout")
            {
                var forgot = players.FirstOrDefault(x => x.uno == UnoState.Forgot);
                if (forgot == null) return;

                if (forgot.id == userId) forgot.uno = UnoState.Said;
                else
                {
                    Message = "";
                    Callout(forgot);
                }
                storage.StoreGame(this);
                return;
            }

            // Set wild color (non-bots)
            else if (Time > 0 && TopCard.Color == CardColor.Black)
            {
                TopCard = new Card(TopCard.Type, Enum.Parse<CardColor>(input, true));
                Message = "";
                ApplyCardEffect();
            }

            // Drawing a card
            else if (input == "draw" || input == "skip" || input == "auto" && CurrentPlayer.cards.All(x => !CanDiscard(x)))
            {
                UnoState previousUno = CurrentPlayer.uno;
                CancelCallouts();
                Draw(CurrentPlayer, 1);

                var drawn = CurrentPlayer.cards.Last();
                if (CanDiscard(drawn))
                {
                    if (!CurrentPlayer.User.IsBot) Message = "";
                    else if (drawn.Color == CardColor.Black) drawn = new Card(drawn.Type, HighestColor(CurrentPlayer.cards));

                    CurrentPlayer.uno = previousUno;
                    Message += $"• {CurrentPlayer.User?.Username} drew and placed {drawn}\n";
                    CurrentPlayer.cards.Pop();
                    Discard(drawn);
                    ApplyCardEffect();
                }
                else
                {
                    if (!CurrentPlayer.User.IsBot) Message = "";

                    updatedPlayers.Add(CurrentPlayer);
                    Message += $"• {CurrentPlayer.User?.Username} drew a card and skipped a turn.\n";
                    Turn = FollowingTurn;
                }
            }

            // Checking and playing a card
            else
            {
                Card card;
                var tryCard = Card.Parse(input, this);

                if (tryCard.HasValue) card = tryCard.Value;
                else
                {
                    if (input.Contains("auto")) Message = "Oops, \"auto\" found no valid matches.";
                    else throw new FormatException($"Unexpected invalid card \"{input}\" from {CurrentPlayer.User.FullName()} in {Channel.FullName()}");
                    return;
                }

                if (!CurrentPlayer.cards.Contains(card.NormalizeColor()))
                {
                    Message = "Oops, you don't have that card!";
                    return;
                }
                else if (!CanDiscard(card))
                {
                    Message = "Oops, that card doesn't match the type or color!";
                    return;
                }

                if (CurrentPlayer.User.IsBot && !AllBots) Message += $"• {CurrentPlayer.User?.Username} plays {card}\n";
                else Message = "";

                if (!CurrentPlayer.User.IsBot) updatedPlayers.Add(CurrentPlayer);

                CurrentPlayer.cards.Remove(card.NormalizeColor());
                Discard(card);

                CancelCallouts();

                if (CurrentPlayer.cards.Count == 1)
                {
                    CurrentPlayer.uno = input.Contains("uno") ? UnoState.Said : UnoState.Forgot;
                }
                else if (CurrentPlayer.cards.Count == 0)
                {
                    State = State.Completed;
                    Winner = Turn;
                    if (!CurrentPlayer.User.IsBot) Message = "";
                    if (CurrentPlayer.id == client.CurrentUser.Id) Message += $"\n {Bot.Random.Choose(WinTexts)}";
                    return;
                }

                ApplyCardEffect();
            }

            updatedPlayers.Add(CurrentPlayer);

            if (!calledByAi || !CurrentPlayer.User.IsBot)
            {
                storage.StoreGame(this);
                foreach (var player in updatedPlayers.Distinct().Where(x => !x.User.IsBot))
                {
                    SendCards(player);
                }
                updatedPlayers = new List<UnoPlayer>();
            }
        }


        public override void BotInput()
        {
            string input = "draw";
            var playable = CurrentPlayer.cards.Where(CanDiscard).ToList();

            if (playable.Count > 0)
            {
                Card choice;

                var niceCards = playable.Where(x => x.Type >= CardType.Skip).ToList();
                if (PlayerAt(FollowingTurn).cards.Count <= 2 && niceCards.Count > 0) choice = Bot.Random.Choose(niceCards);
                else
                {
                    var noWilds = playable.Where(x => x.Color != CardColor.Black).ToList();
                    choice = Bot.Random.Choose(noWilds.Count > 0 ? noWilds : playable); // Leaves wilds for last
                }

                if (choice.Color == CardColor.Black && CurrentPlayer.cards.Count > 1)
                {
                    choice = new Card(choice.Type, HighestColor(CurrentPlayer.cards));
                }

                input = choice.ToString();
                if (CurrentPlayer.cards.Count == 2 && !Bot.Random.OneIn(10)) input += "uno"; // Sometimes "forgets" to say uno
            }

            var forgot = players.FirstOrDefault(x => x.uno == UnoState.Forgot);
            if (forgot != null && (forgot.User.IsBot || !Bot.Random.OneIn(3))) // Sometimes calls out
            {
                Callout(forgot);
            }

            Input(input);
        }

        private static CardColor HighestColor(List<Card> cards)
        {
            var colors = Enumerable.Range(0, 4).Select(x => cards.Count(c => c.Color == (CardColor)x)).ToList();
            return (CardColor)colors.IndexOf(Bot.Random.Choose(colors.Where(x => x == colors.Max()).ToList()));
        }



        public override EmbedBuilder GetEmbed(bool showHelp = true)
        {
            if (State == State.Cancelled) return CancelledEmbed();

            var description = new StringBuilder();
            string prefix = storage.GetPrefixOrEmpty(Guild);

            if (players.Count < 2) description.Append("👥 You need at least 2 players.\nWait for friends to join or invite some bots!\n\n");

            description.Append("```diff\n");
            for (int i = 0; i < players.Count; i++)
            {
                string pre = i == (int)Turn ? "+ " : Winner != Player.None ? "- " : "";
                description.Append($"{pre}{players[i].User?.Username}");
                if (players[i].cards.Count > 0) description.Append($" - {players[i].cards.Count}🃏{" Uno!".If(players[i].uno == UnoState.Said)}");
                description.Append('\n');
            }
            description.Append($"\n```\n{TopCard.ToStringBig()}\n");

            if (State == State.Active)
            {
                description.Append(
                    "ᅠ\nSay the name of a card to discard it or \"draw\" to skip a turn." +
                    "\nYour cards are shown in a DM, say \"cards\" to resend." +
                    $"\nUse **{prefix}uno join** to join the game.\nUse **{prefix}uno help** for rules and more commands.");
            }


            return new EmbedBuilder()
            {
                Title = Winner == Player.None
                    ? $"{(reversed ? "🔼" : "🔽")} {CurrentPlayer.User?.Username}'s turn"
                    : $"🎉 {CurrentPlayer.User?.Username} is the winner! 🎉",

                Description = description.ToString().Truncate(2047),
                Color = RgbCardColor[(int)TopCard.Color],
                ThumbnailUrl = CurrentPlayer.User?.GetAvatarUrl(),
            };
        }


        public override string GetContent(bool showHelp = true)
        {
            return State == State.Cancelled ? "" : Message?.TruncateStart(2000);
        }




        private bool CanDiscard(Card card)
        {
            return card.WildType || TopCard.Color == card.Color || TopCard.Type == card.Type || TopCard.Color == CardColor.Black;
        }


        private void Discard(Card card)
        {
            discardPile.Insert(0, card);
            Time++;
        }


        private void Draw(UnoPlayer player, int amount)
        {
            player.uno = UnoState.None;
            while (amount > 0)
            {
                if (drawPile.Count == 0)
                {
                    Bot.Random.Shuffle(discardPile);
                    drawPile = discardPile.ToList();
                    discardPile = new List<Card> { drawPile.Pop() };
                    Message += "• Shuffled and turned over the discard pile.\n";

                    if (drawPile.Count == 0) break; // No more cards aaaaaa!
                }

                player.cards.Add(drawPile.Pop().NormalizeColor());
                amount--;
            }
        }


        private void ApplyCardEffect()
        {
            var card = TopCard;

            if (Time > 0 && card.Color == CardColor.Black)
            {
                Message += $"• {CurrentPlayer.User?.Mention} choose a color: red/blue/green/yellow\n";
                return;
            }


            switch (card.Type)
            {
                case CardType.Skip:
                case CardType.Reverse when players.Count == 2:
                    Turn = FollowingTurn;
                    Message += $"• {CurrentPlayer.User?.Username} skips a turn!\n";
                    break;

                case CardType.Reverse:
                    reversed = !reversed;
                    Message += $"• Now going {(reversed ? "backwards" : "forwards")}!\n";
                    break;

                case CardType.DrawTwo:
                case CardType.WildDrawFour:
                    Turn = FollowingTurn;
                    Draw(CurrentPlayer, card.CardsToDraw);
                    Message += $"• {CurrentPlayer.User?.Username} draws {card.CardsToDraw} cards and skips a turn!\n";
                    break;
            }

            if (Time > 0)
            {
                Turn = FollowingTurn;
            }
        }


        private void Callout(UnoPlayer player)
        {
            Draw(player, 2);
            Message += $"• {player.User.Username} was called out for not saying Uno and drew 2 cards!\n";
            updatedPlayers.Add(player);
        }


        private void CancelCallouts()
        {
            foreach (var player in players)
            {
                if (player.uno == UnoState.Forgot) player.uno = UnoState.NotCalledOut;
            }
        }




        private void SendCards(UnoPlayer player)
        {
            if (player.User == null || player.User.IsBot) return;

            var cardsByColor = player.cards.ToList().Sorted().GroupBy(x => x.Color);

            string cardList = "";
            foreach (var group in cardsByColor)
            {
                cardList += $"{CardColorEmote[(int)group.Key]} {string.Join(", ", group)}\n";
            }
            cardList += "ᅠ";

            var embed = new EmbedBuilder
            {
                Title = $"{Name} game in #{Channel.Name}{(Guild == null ? "" : $" ({Guild.Name})")}",
                Description = "Send the name of a card in the game's channel to discard that card." +
                              "\nIf you can't or don't want to choose any card, say \"draw\" instead." +
                              "\nYou can also use \"auto\" instead of a color/number/both.\nᅠ",
                Color = RgbCardColor[(int)TopCard.Color],
            };

            embed.AddField("Your cards", cardList.Truncate(1023));
            embed.AddField("Top of the pile", TopCard);

            bool resend = false;
            if (player.message == null) resend = true;
            else
            {
                try { player.message.ModifyAsync(m => m.Embed = embed.Build(), Bot.DefaultOptions).GetAwaiter().GetResult(); }
                catch (HttpException) { resend = true; }
            }

            if (resend)
            {
                try
                {
                    player.message = player.User.SendMessageAsync(embed: embed.Build(), options: Bot.DefaultOptions).GetAwaiter().GetResult();
                }
                catch (HttpException e) when (e.DiscordCode == 50007) // Can't send DMs
                {
                    Message = $"{player.User?.Mention} You can't play unless you have DMs enabled!";
                    RemovePlayer(player);
                } 
            }
        }



        public string AddPlayer(ulong id)
        {
            if (players.Count == 10) return "The game is full!";
            if (UserId.Contains(id)) return "You're already playing!";

            var player = new UnoPlayer(id, client);
            players.Add(player);
            Draw(player, CardsPerPlayer);
            SendCards(player);

            return null;
        }


        public void RemovePlayer(ulong id) => RemovePlayer(players.First(x => x.id == id));
        private void RemovePlayer(UnoPlayer player)
        {
            drawPile.AddRange(player.cards);
            players.Remove(player);

            while ((int)Turn >= players.Count || !AllBots && CurrentPlayer.User.IsBot) Turn = FollowingTurn;
        }




        public void PostDeserialize(DiscordShardedClient client, LoggingService logger, StorageService storage)
        {
            this.client = client;
            this.logger = logger;
            this.storage = storage;

            foreach (var player in players) player.client = client;
        }
    }
}
