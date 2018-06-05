using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Discord;
using Discord.WebSocket;
using PacManBot.Constants;
using PacManBot.Services;
using static PacManBot.Games.GameUtils;
using Discord.Net;

namespace PacManBot.Games
{
    [DataContract]
    public class UnoGame : MultiplayerGame, IMessagesGame, IStoreableGame
    {
        // Constants

        const int CardsPerPlayer = 7;
        private static readonly TimeSpan _expiry = TimeSpan.FromHours(24);


        // Fields

        [DataMember] private List<ulong> players = new List<ulong>();
        [DataMember] private List<Card> drawPile = new List<Card>();
        [DataMember] private List<Card> discardPile = new List<Card>();
        [DataMember] private List<List<Card>> playerCards = new List<List<Card>>();
        [DataMember] private bool reversed = false;
        private List<IUserMessage> cardsMessages = new List<IUserMessage>();


        // Properties

        public override string Name => "Uno";
        public override TimeSpan Expiry => _expiry;
        public override bool AITurn => players.Count > 1 && base.AITurn;
        public string FilenameKey => "uno";

        private Card TopCard => discardPile[0];

        [DataMember] public override State State { get; set; }
        [DataMember] public override DateTime LastPlayed { get; set; }
        [DataMember] public override int Time { get; set; }
        [DataMember] public override ulong MessageId { get; set; }
        [DataMember] public override ulong ChannelId { get; set; }
        [DataMember] public override Player Turn { get; protected set; }
        [DataMember] public override Player Winner { get; protected set; }

        public override ulong[] UserId
        {
            get { return players.ToArray(); }
            set { players = value.ToList(); }
        }



        [DataContract]
        struct Card : IComparable<Card>
        {
            [DataMember] public CardType Type { get; private set; }
            [DataMember] public CardColor Color { get; private set; }

            public bool WildType => Type == CardType.Wild || Type == CardType.WildDrawFour;

            public static readonly Card Wild = new Card(CardType.Wild, CardColor.Black);
            public static readonly Card WildDrawFour = new Card(CardType.WildDrawFour, CardColor.Black);

            public static readonly Color[] RgbColor = new Color[] {
                new Color(221, 46, 68), new Color(85, 172, 238), new Color(120, 177, 89), new Color(253, 203, 88), new Color(20, 26, 30)
            };

            public static readonly string[] TypeEmote = CustomEmoji.NumberCircle
                .Union(new Emote[] { CustomEmoji.UnoSkip, CustomEmoji.UnoReverse, CustomEmoji.AddTwo, CustomEmoji.AddFour, CustomEmoji.UnoWild })
                .Select(x => x.ToString()).ToArray();


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


            public static Card? FromString(string value)
            {
                value = value.ToLower().Replace(" ", "").Replace("draw2", "drawtwo").Replace("draw4", "drawfour");
                CardColor? color = null;
                CardType? type = null;
                
                for (int c = 0; c < 4; c++)
                {
                    string colorStr = ((CardColor)c).ToString().ToLower();
                    if (value.StartsWith(colorStr) || value.EndsWith(colorStr))
                    {
                        color = (CardColor)c;
                        value = value.Replace(colorStr, "");
                        break;
                    }
                }

                for (int t = 0; t < 15; t++)
                {
                    string typeStr = ((CardType)t).ToString().ToLower();
                    if (value == typeStr || t < 10 && value == t.ToString())
                    {
                        type = (CardType)t;
                        if (color == null && (type == CardType.WildDrawFour || type == CardType.Wild)) color = CardColor.Black;
                        break;
                    }
                }

                if (type != null) return new Card(type.Value, color.Value);
                else return null;
            }


            public int CompareTo(Card other)
            {
                int result = ((int)Color).CompareTo((int)other.Color);
                if (result == 0) result = ((int)Type).CompareTo((int)other.Type);
                return result;
            }
        }

        enum CardColor
        {
            Red, Blue, Green, Yellow, Black,
        }

        enum CardType
        {
            Zero, One, Two, Three, Four, Five, Six, Seven, Eight, Nine,
            Skip, Reverse, DrawTwo, WildDrawFour, Wild
        }




        public override void Create(ulong channelId, ulong[] playerIds, DiscordShardedClient client, LoggingService logger, StorageService storage)
        {
            base.Create(channelId, new ulong[] { }, client, logger, storage);

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

            foreach (ulong id in playerIds) AddPlayer(id);

            while (TopCard.Type == CardType.WildDrawFour || playerCards[0].All(x => !CanPlace(x))) // Invalid first card
            {
                var index = Bot.Random.Next(drawPile.Count);
                var temp = drawPile[index];
                drawPile[index] = discardPile[0];
                discardPile[0] = temp;
            }

            if (TopCard.Type == CardType.Reverse) reversed = true;
            if (TopCard.Type == CardType.Skip || TopCard.Type == CardType.Reverse && players.Count == 2) Turn = FollowingPlayer();
        }



        public bool IsInput(string value, ulong userId)
        {
            if (players.Contains(userId))
            {
                if (value.ToLower() == "cards")
                {
                    SendCards(players.IndexOf(userId));
                    return true;
                }

                return userId == User(Turn)?.Id && players.Count >= 2 && Card.FromString(StripPrefix(value)).HasValue;
            }

            return false;
        }


        public void DoTurn(string input)
        {
            if (input.ToLower() == "cards") return;

            var card = Card.FromString(StripPrefix(input)).Value;

            if (!playerCards[(int)Turn].Contains(card.NormalizeColor()))
            {
                Message = $"Oops, you don't have that card!";
                return;
            }
            else if (card.Color == CardColor.Black)
            {
                Message = $"You must specify a color for your wild card!";
                return;
            }
            else if (!CanPlace(card))
            {
                Message = $"Oops, that card doesn't match the type or color!";
                return;
            }

            bool saved = false;
            if (!User(Turn).IsBot)
            {
                storage.StoreGame(this);
                saved = true;
            }

            if (User(Turn).IsBot && !AllBots) Message += $"• {User(Turn)?.Username} plays {input}\n";
            else Message = "";

            playerCards[(int)Turn].Remove(card.NormalizeColor());
            discardPile.Insert(0, card);
            Time++;

            if (playerCards[(int)Turn].Count == 0)
            {
                State = State.Completed;
                Winner = Turn;
                if (!User(Winner).IsBot) Message = "";
                Message += User(Winner).Id == client.CurrentUser.Id ? Bot.Random.Choose(WinTexts) : "";
                return;
            }

            SendCards(Turn);

            if (card.Type == CardType.Reverse)
            {
                reversed = !reversed;
                if (players.Count > 2) Message += $"• Now going {(reversed ? "backwards" : "forwards")}!\n";
            }

            Turn = FollowingPlayer();
            bool sentCards = false;

            if (card.Type == CardType.Skip || card.Type == CardType.Reverse && players.Count == 2)
            {
                Message += $"• {User(Turn)?.Username} skips a turn!\n";
                Turn = FollowingPlayer();
            }
            else if (card.Type == CardType.WildDrawFour || card.Type == CardType.DrawTwo)
            {
                int amount = card.Type == CardType.WildDrawFour ? 4 : 2;
                Draw(Turn, amount);
                Message += $"• {User(Turn)?.Username} draws {amount} cards and skips a turn!\n";
                Turn = FollowingPlayer();
            }

            while (playerCards[(int)Turn].All(x => !CanPlace(x))) // Next player can't place
            {
                Draw(Turn, 1);
                if (CanPlace(playerCards[(int)Turn].Last()))
                {
                    Message += $"• {User(Turn)?.Username} couldn't play and drew a card.\n";
                    break;
                }
                else
                {
                    Message += $"• {User(Turn)?.Username} couldn't play, drew a card and skipped a turn.\n";
                    Turn = FollowingPlayer();
                }
            }

            if (!sentCards) SendCards(Turn);

            if (!saved && !User(Turn).IsBot)
            {
                storage.StoreGame(this);
            }
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
                string pre = i == (int)Turn ? "+ " : Winner == Player.None ? "* " : "- ";
                description.Append($"{pre}{User(i)?.Username} - {playerCards[i].Count}🃏{$" Uno!".If(playerCards[i].Count == 1)}\n");
            }
            description.Append($"```\n{TopCard.ToStringBig()}\n");

            if (State == State.Active) description.Append($"ᅠ\nSay the name of a card to place it on top of this one.\nYour cards are shown in a DM, say \"cards\" to resend.\nUse **{prefix}uno join** to join the game.\nUse **{prefix}uno help** for rules and more commands.");


            return new EmbedBuilder()
            {
                Title = Winner == Player.None ? $"{(reversed ? "🔼" : "🔽")} {User(Turn)?.Username}'s turn" : $"🎉 {User(Turn)?.Username} is the winner! 🎉",
                Description = description.ToString().Truncate(2047),
                Color = Card.RgbColor[(int)TopCard.Color],
                ThumbnailUrl = User(Turn)?.GetAvatarUrl(),
            };
        }


        public override string GetContent(bool showHelp = true)
        {
            return State == State.Cancelled ? "" : Message.Truncate(1999);
        }




        public override void DoTurnAI()
        {
            Card choice;

            var playable = playerCards[(int)Turn].Where(x => CanPlace(x)).ToList();
            var niceCards = playable.Where(x => x.Type >= CardType.Skip).ToList();

            if (playerCards[(int)FollowingPlayer()].Count <= 2 && niceCards.Count > 0) choice = Bot.Random.Choose(niceCards);
            else
            {
                var noWilds = playable.Where(x => x.Color != CardColor.Black).ToList();
                choice = Bot.Random.Choose(noWilds.Count > 0 ? noWilds : playable); // Leaves wilds for last
            }

            if (choice.Color == CardColor.Black)
            {
                var colors = Enumerable.Range(0, 4).Select(x => playerCards[(int)Turn].Count(c => c.Color == (CardColor)x)).ToList();
                choice = new Card(choice.Type, (CardColor)colors.IndexOf(Bot.Random.Choose(colors.Where(x => x == colors.Max()).ToList())));
            }

            DoTurn(choice.ToString());
        }




        private Player FollowingPlayer()
        {
            return reversed ? PreviousPlayer() : NextPlayer();
        }


        private bool CanPlace(Card card)
        {
            return card.WildType || TopCard.Color == card.Color || TopCard.Type == card.Type || TopCard.Color == CardColor.Black; // The black TopCard can only happen at the start
        }


        private void Draw(Player player, int amount) => Draw(playerCards[(int)player], amount);
        private void Draw(List<Card> cards, int amount)
        {
            while (amount > 0)
            {
                if (drawPile.Count == 0)
                {
                    Message += "• Shuffled and turned over the discard pile.\n";
                    Bot.Random.Shuffle(discardPile);
                    drawPile = discardPile.ToList();
                    discardPile = new List<Card> { drawPile.Pop() };

                    if (drawPile.Count == 0) break; // No more cards aaaaaa!
                }

                cards.Add(drawPile.Pop().NormalizeColor());
                amount--;
            }
        }


        private void SendCards(Player player) => SendCards((int)player);
        private void SendCards(int player)
        {
            while (players.Count > cardsMessages.Count) cardsMessages.Add(null);

            if (User(player) == null || User(player).IsBot) return;

            var cardsByColor = playerCards[player].GroupBy(x => x.Color).ToList().OrderBy(x => (int)x.Key).ToList();

            string cardList = "";
            foreach (var group in cardsByColor)
            {
                cardList += $"{CustomEmoji.ColorSquare[(int)group.Key]} {string.Join(", ", group.OrderBy(x => (int)x.Type))}\n";
            }
            cardList += "ᅠ";

            var embed = new EmbedBuilder
            {
                Title = $"{Name} game in #{Channel.Name}{(Guild == null ? "" : $" ({Guild.Name})")}",
                Description = "*Send the name of a card in that channel to place it*\nᅠ",
                Color = Time == 0 ? new Color?() : Card.RgbColor[(int)TopCard.Color],
            };

            embed.AddField("Your cards", cardList.Truncate(1023));
            if (Time > 0) embed.AddField("Top of the pile", TopCard);

            bool resend = false;
            if (cardsMessages[player] == null) resend = true;
            else
            {
                try { cardsMessages[player].ModifyAsync(m => m.Embed = embed.Build(), Utils.DefaultOptions).GetAwaiter().GetResult(); }
                catch (HttpException) { resend = true; }
            }

            if (resend)
            {
                try { cardsMessages[player] = User(player).SendMessageAsync("", false, embed.Build(), options: Utils.DefaultOptions).GetAwaiter().GetResult(); }
                catch (HttpException) { RemovePlayer(player); } // Can't send DMs
            }
        }



        public string AddPlayer(ulong id)
        {
            if (players.Count == 10) return "The game is full!";
            if (players.Contains(id)) return "You're already playing!";

            players.Add(id);
            var cards = new List<Card>();
            Draw(cards, CardsPerPlayer);
            playerCards.Add(cards);
            SendCards(playerCards.Count - 1);

            return null;
        }


        public void RemovePlayer(ulong id) => RemovePlayer(players.IndexOf(id));
        private void RemovePlayer(int index)
        {
            players.RemoveAt(index);
            drawPile.AddRange(playerCards[index]);
            playerCards.RemoveAt(index);
            cardsMessages.RemoveAt(index);

            if (index == players.Count) Turn = FollowingPlayer();
        }


        public void SetServices(DiscordShardedClient client, LoggingService logger, StorageService storage)
        {
            this.client = client;
            this.logger = logger;
            this.storage = storage;
        }
    }
}
