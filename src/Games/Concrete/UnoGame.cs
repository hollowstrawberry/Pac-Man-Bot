using System;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Discord;
using Discord.Net;
using Discord.WebSocket;
using PacManBot.Services;
using PacManBot.Extensions;

namespace PacManBot.Games
{
    [DataContract]
    public class UnoGame : MultiplayerGame, IMessagesGame, IStoreableGame
    {
        // Constants

        const int CardsPerPlayer = 7;
        private static readonly TimeSpan _expiry = TimeSpan.FromHours(24);


        // Fields

        [DataMember] private List<UnoPlayer> players = new List<UnoPlayer>();
        [DataMember] private List<Card> drawPile = new List<Card>();
        [DataMember] private List<Card> discardPile = new List<Card>();
        [DataMember] private bool reversed = false;
        [IgnoreDataMember] private List<UnoPlayer> updatedCards = new List<UnoPlayer>();


        // Properties

        public override string Name => "Uno";
        public override TimeSpan Expiry => _expiry;
        public override bool BotTurn => players.Count > 1 && base.BotTurn;
        public string FilenameKey => "uno";
        public override bool AllBots => players.All(x => x.User?.IsBot ?? false);

        private Card TopCard => discardPile[0];
        private UnoPlayer CurrentPlayer => players[(int)Turn];
        private UnoPlayer PlayerAt(Player turn) => players[(int)turn];
        private Player FollowingTurn => reversed ? PreviousPlayer() : NextPlayer();
        private Player PrecedingTurn => reversed ? NextPlayer() : PreviousPlayer();

        [DataMember] public override State State { get; set; }
        [DataMember] public override DateTime LastPlayed { get; set; }
        [DataMember] public override int Time { get; set; }
        [DataMember] public override ulong ChannelId { get; set; }
        [DataMember] public override ulong MessageId { get; set; } = 1;
        [DataMember] public override Player Turn { get; protected set; }
        [DataMember] public override Player Winner { get; protected set; }

        public override ulong[] UserId
        {
            get { return players.Select(x => x.id).ToArray(); }
            set { throw new InvalidOperationException("Use AddPlayer and RemovePlayer instead"); }
        }



        // Types

        enum UnoState
        {
            None, Said, Forgot, NotCalledOut,
        }

        enum CardColor
        {
            Red, Blue, Green, Yellow, Black,
        }

        enum CardType
        {
            Zero, One, Two, Three, Four, Five, Six, Seven, Eight, Nine,
            Skip, Reverse, DrawTwo, WildDrawFour, Wild,
        }



        [DataContract]
        class UnoPlayer
        {
            [DataMember] public readonly ulong id;
            [DataMember] public List<Card> cards = new List<Card>();
            [DataMember] public UnoState uno = UnoState.None;

            public DiscordShardedClient client;
            public IUserMessage message = null;
            public IUser User
            {
                get
                {
                    if (_user == null) _user = client.GetUser(id);
                    return _user;
                }
            }

            private IUser _user = null;


            private UnoPlayer() { } // Used in serialization

            public UnoPlayer(ulong id, DiscordShardedClient client)
            {
                this.id = id;
                _user = client.GetUser(id);
            }
        }


        [DataContract]
        struct Card : IComparable<Card>
        {
            public static readonly Card Wild = new Card(CardType.Wild, CardColor.Black);
            public static readonly Card WildDrawFour = new Card(CardType.WildDrawFour, CardColor.Black);

            public static readonly string[] StrColor = Enumerable.Range(0, 5).Select(x => ((CardColor)x).ToString().ToLower()).ToArray();
            public static readonly string[] StrType = Enumerable.Range(0, 15).Select(x => ((CardType)x).ToString().ToLower()).ToArray();
            public static readonly Color[] RgbColor = new Color[] {
                new Color(221, 46, 68), new Color(85, 172, 238), new Color(120, 177, 89), new Color(253, 203, 88), new Color(20, 26, 30),
            };
            public static readonly string[] TypeEmote = CustomEmoji.NumberCircle.Concatenate(new string[] {
                CustomEmoji.UnoSkip, CustomEmoji.UnoReverse, CustomEmoji.AddTwo, CustomEmoji.AddFour, CustomEmoji.UnoWild
            });


            [DataMember] public CardType Type { get; private set; }
            [DataMember] public CardColor Color { get; private set; }

            public bool WildType => IsWild(Type);


            public Card(CardType Type, CardColor Color)
            {
                this.Type = Type;
                this.Color = Color;
            }


            public Card NormalizeColor()
            {
                if (WildType && Color != CardColor.Black) return new Card(Type, CardColor.Black);
                else return this;
            }


            public override string ToString()
            {
                string typeStr = Type > CardType.Nine ? $"{Type}" : $"{((int)Type)}";
                string colorStr = Color == CardColor.Black ? "" : Color.ToString();

                if (WildType) return $"{typeStr} {colorStr}";
                else return colorStr + typeStr;
            }

            public string ToStringBig()
            {
                var card = new StringBuilder();
                for (int y = 0; y < 5; y++)
                {
                    card.Append($"{CustomEmoji.Empty}".Multiply(2));
                    for (int x = 0; x < 3; x++)
                    {
                        if (x == 1 && y == 2) card.Append(TypeEmote[(int)Type]);
                        else card.Append(CustomEmoji.ColorSquare[(int)Color]);
                    }
                    card.Append('\n');
                }
                card.Append($"{CustomEmoji.Empty}".Multiply(2));
                card.Append(this.ToString());

                return card.ToString();
            }


            public static bool IsWild(CardType type) => type == CardType.Wild || type == CardType.WildDrawFour;


            public static Card? Parse(string value, UnoGame game)
            {
                bool auto = value.Contains("auto");
                value = value.Replace(" ", "").Replace("auto", "").Replace("uno", "").Replace("draw2", "drawtwo").Replace("draw4", "drawfour");
                
                CardColor? color = (CardColor?)Enumerable.Range(0, 4).FirstOrNull(x => value.StartsWith(StrColor[x]) || value.EndsWith(StrColor[x]));
                if (color.HasValue) value = value.Replace(StrColor[(int)color], "");
                CardType? type = (CardType?)Enumerable.Range(0, 15).FirstOrNull(x => value == StrType[x] || x < 10 && value == x.ToString());

                if (auto)
                {
                    var cards = game.CurrentPlayer.cards.ToList().Sorted();

                    if (color == null && type == null) return value == "" ? cards.FirstOrNull(x => game.CanPlace(x)) : null;
                    else if (color == null) return cards.FirstOrNull(x => x.Type == type && game.CanPlace(x));
                    else if (type == null) return cards.FirstOrNull(x => x.Color == color && game.CanPlace(x));
                }

                if (color == null && type.HasValue && IsWild(type.Value)) color = CardColor.Black;

                if (type.HasValue && color.HasValue) return new Card(type.Value, color.Value);
                else return null;
            }


            public int CompareTo(Card other)
            {
                int result = ((int)Color).CompareTo((int)other.Color); // Sorts by color ascending
                if (result == 0) result = ((int)other.Type).CompareTo(((int)Type)); // Then by type descending
                return result;
            }
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

            var mentions = playerIds.Skip(1).Where(x => !client.GetUser(x).IsBot).Distinct().Take(9).ToArray();
            foreach (ulong id in playerIds.Where(x => !mentions.Contains(x))) AddPlayer(id);

            if (players.Count > 1) Message = $"• First card is {TopCard}\n";

            Time = -1;
            DiscardedCardLogic();

            if (mentions.Length > 0)
            {
                string invite = $"{string.Join(", ", mentions.Select(x => client.GetUser(x)?.Mention))} You've been invited to play Uno. Type `{storage.GetPrefix(Guild)}uno join` to join.\n";
                Message = invite + Message;
            }
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
                    if (Time > 0 && TopCard.Color == CardColor.Black) return Enumerable.Range(0, 4).Any(x => value == Card.StrColor[x]); // Wild color
                    else return value == "draw" || value == "skip" || value.Contains("auto") || Card.Parse(value, this).HasValue;
                }
            }

            return false;
        }


        public void Input(string input, ulong userId = 1)
        {
            input = StripPrefix(input.ToLower());
            bool calledByAI = CurrentPlayer.User.IsBot;


            // Send cards
            if (input == "cards")
            {
                SendCards(players.First(x => x.User?.Id == userId));
                return;
            }

            // Saying "uno" mechanics
            else if (input == "uno" || input == "callout")
            {
                var forgot = players.FirstOrDefault(x => x.uno == UnoState.Forgot);
                if (forgot == null) return;

                if (forgot.id == userId) forgot.uno = UnoState.Said;
                else
                {
                    Draw(forgot, 2);
                    Message = $"• {forgot.User.Username} was called out for not saying Uno and drew 2 cards!\n";
                    updatedCards.Add(forgot);
                }
                storage.StoreGame(this);
                return;
            }

            // Set wild color (non-bots)
            else if (Time > 0 && TopCard.Color == CardColor.Black)
            {
                discardPile[0] = new Card(TopCard.Type, (CardColor)Enumerable.Range(0, 4).First(x => input == Card.StrColor[x]));
                Turn = FollowingTurn;
                Time++;
                Message = "";

                if (TopCard.Type == CardType.WildDrawFour)
                {
                    Draw(PlayerAt(FollowingTurn), 4);
                    Message += $"• {PlayerAt(FollowingTurn).User?.Username} draws 4 cards and skips a turn!\n";
                }
            }

            // Drawing a card
            else if (input == "draw" || input == "skip" || input == "auto" && CurrentPlayer.cards.All(x => !CanPlace(x)))
            {
                CancelCallouts();
                Draw(CurrentPlayer, 1);

                var drawn = CurrentPlayer.cards.Last();
                if (CanPlace(drawn))
                {
                    if (!CurrentPlayer.User.IsBot) Message = "";
                    else if (drawn.Color == CardColor.Black) drawn = new Card(drawn.Type, HighestColor(CurrentPlayer.cards));

                    Message += $"• {CurrentPlayer.User?.Username} drew and placed {drawn}\n";
                    CurrentPlayer.cards.Pop();
                    discardPile.Insert(0, drawn);

                    if (drawn.Color == CardColor.Black) Message += $"• {CurrentPlayer.User?.Mention} choose a color: red/blue/green/yellow\n";
                    else DiscardedCardLogic();
                }
                else
                {
                    if (!CurrentPlayer.User.IsBot) Message = "";

                    updatedCards.Add(CurrentPlayer);
                    Message += $"• {CurrentPlayer.User?.Username} drew a card and skipped a turn.\n";
                    Turn = FollowingTurn;
                    Time++;
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
                    if (input.Contains("auto")) Message = $"Oops, \"auto\" found no valid matches.";
                    else logger.Log(LogSeverity.Error, Name, $"Unexpected invalid card \"{input}\" from {CurrentPlayer.User.FullName()} in {Channel.FullName()}");
                    return;
                }

                if (!CurrentPlayer.cards.Contains(card.NormalizeColor()))
                {
                    Message = $"Oops, you don't have that card!";
                    return;
                }
                else if (!CanPlace(card))
                {
                    Message = $"Oops, that card doesn't match the type or color!";
                    return;
                }

                if (CurrentPlayer.User.IsBot && !AllBots) Message += $"• {CurrentPlayer.User?.Username} plays {card}\n";
                else Message = "";

                if (!CurrentPlayer.User.IsBot) updatedCards.Add(CurrentPlayer);

                CurrentPlayer.cards.Remove(card.NormalizeColor());
                discardPile.Insert(0, card);

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

                DiscardedCardLogic();
            }

            updatedCards.Add(CurrentPlayer);

            if (!calledByAI || !CurrentPlayer.User.IsBot)
            {
                storage.StoreGame(this);
                foreach (var player in updatedCards.Distinct().Where(x => !x.User.IsBot)) SendCards(player);
                updatedCards = new List<UnoPlayer>();
            }
        }


        private void DiscardedCardLogic()
        {
            if (TopCard.Type == CardType.Reverse)
            {
                reversed = !reversed;
                if (players.Count > 2) Message += $"• Now going {(reversed ? "backwards" : "forwards")}!\n";
            }

            Time++;
            if (Time > 0) // Ignores turn at the start of the game
            {
                if (TopCard.Color == CardColor.Black)
                {
                    Message += $"• {CurrentPlayer.User?.Mention} choose a color: red/blue/green/yellow\n";
                    return;
                }
                else
                {
                    Turn = FollowingTurn;
                }
            }

            if (players.Count <= 1) return;

            if (TopCard.Type == CardType.Skip || TopCard.Type == CardType.Reverse && players.Count == 2)
            {
                Message += $"• {CurrentPlayer.User?.Username} skips a turn!\n";
                Turn = FollowingTurn;
                Time++;
            }
            else if (TopCard.Type == CardType.WildDrawFour || TopCard.Type == CardType.DrawTwo)
            {
                int amount = TopCard.Type == CardType.WildDrawFour ? 4 : 2;
                Draw(CurrentPlayer, amount);
                Message += $"• {CurrentPlayer.User?.Username} draws {amount} cards and skips a turn!\n";
                Turn = FollowingTurn;
                Time++;
            }
        }


        public override void BotInput()
        {
            string input = "draw";

            var playable = CurrentPlayer.cards.Where(x => CanPlace(x)).ToList();

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

                if (choice.Color == CardColor.Black && CurrentPlayer.cards.Count > 1) choice = new Card(choice.Type, HighestColor(CurrentPlayer.cards));

                input = choice.ToString();
                if (CurrentPlayer.cards.Count == 2 && !Bot.Random.OneIn(10)) input += "uno"; // Sometimes "forgets" to say uno
            }

            var forgot = players.FirstOrDefault(x => x.uno == UnoState.Forgot);
            if (forgot != null && (forgot.User.IsBot || !Bot.Random.OneIn(3))) // Sometimes calls out
            {
                Draw(forgot, 2);
                Message += $"• {forgot.User.Username} was called out for not saying Uno and drew 2 cards!\n";
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
                description.Append($"{pre}{players[i].User?.Username}" + $" - {players[i].cards.Count}🃏{$" Uno!".If(players[i].uno == UnoState.Said)}".If(players[i].cards.Count > 0) + "\n");
            }
            description.Append($"```\n{TopCard.ToStringBig()}\n");

            if (State == State.Active) description.Append($"ᅠ\nSay the name of a card to discard it or \"draw\" to skip a turn.\nYour cards are shown in a DM, say \"cards\" to resend.\nUse **{prefix}uno join** to join the game.\nUse **{prefix}uno help** for rules and more commands.");


            return new EmbedBuilder()
            {
                Title = Winner == Player.None ? $"{(reversed ? "🔼" : "🔽")} {CurrentPlayer.User?.Username}'s turn" : $"🎉 {CurrentPlayer.User?.Username} is the winner! 🎉",
                Description = description.ToString().Truncate(2047),
                Color = Card.RgbColor[(int)TopCard.Color],
                ThumbnailUrl = CurrentPlayer.User?.GetAvatarUrl(),
            };
        }


        public override string GetContent(bool showHelp = true)
        {
            return State == State.Cancelled ? "" : Message?.Truncate(1999);
        }




        private bool CanPlace(Card card)
        {
            return card.WildType || TopCard.Color == card.Color || TopCard.Type == card.Type || TopCard.Color == CardColor.Black; // The black TopCard can only happen at the start
        }


        private void CancelCallouts()
        {
            foreach (var player in players)
            {
                if (player.uno == UnoState.Forgot) player.uno = UnoState.NotCalledOut;
            }
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


        private void SendCards(UnoPlayer player)
        {
            if (player.User == null || player.User.IsBot) return;

            var cardsByColor = player.cards.ToList().Sorted().GroupBy(x => x.Color);

            string cardList = "";
            foreach (var group in cardsByColor)
            {
                cardList += $"{CustomEmoji.ColorSquare[(int)group.Key]} {string.Join(", ", group)}\n";
            }
            cardList += "ᅠ";

            var embed = new EmbedBuilder
            {
                Title = $"{Name} game in #{Channel.Name}{(Guild == null ? "" : $" ({Guild.Name})")}",
                Description = "Send the name of a card in the game's channel to discard that card.\nIf you don't have any matching card or don't want to play, say \"draw\" instead.\nYou can also use \"auto\" instead of a color/number/both.\nᅠ",
                Color = Time == 0 ? default(Color?) : Card.RgbColor[(int)TopCard.Color],
            };

            embed.AddField("Your cards", cardList.Truncate(1023));
            if (Time > 0) embed.AddField("Top of the pile", TopCard);

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
                    player.message = player.User.SendMessageAsync("", false, embed.Build(), options: Bot.DefaultOptions).GetAwaiter().GetResult();
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
