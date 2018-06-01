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
        [DataMember] private List<Card> deck = new List<Card>();
        [DataMember] private List<List<Card>> playerCards = new List<List<Card>>();
        [DataMember] private bool reversed = false;
        private List<IUserMessage> cardsMessages = new List<IUserMessage>();


        // Properties

        public override string Name => "Uno";
        public override TimeSpan Expiry => _expiry;
        public override bool AITurn => players.Count > 1 && base.AITurn;
        public string GameFile => $"{GameFolder}uno{ChannelId}{GameExtension}";

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
        public struct Card : IComparable<Card>
        {
            [DataMember] public CardType Type { get; private set; }
            [DataMember] public CardColor Color { get; private set; }

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
                this.Color = Type == CardType.WildDrawFour || Type == CardType.Wild ? CardColor.Black : Color;
            }


            public override string ToString()
            {
                return Color == CardColor.Black ? $"{Type}" : $"{Color}{(Type <= CardType.Nine ? $"{((int)Type)}" : $"{Type}")}";
            }

            public string ToStringBig()
            {
                string card = "";
                for (int y = 0; y < 5; y++)
                {
                    card += $"{CustomEmoji.Empty}".Multiply(2);
                    for (int x = 0; x < 3; x++)
                    {
                        if (x == 1 && y == 2) card += TypeEmote[(int)Type];
                        else card += CustomEmoji.ColorSquare[(int)Color];
                    }
                    card += '\n';
                }

                return card + $"{CustomEmoji.Empty}".Multiply(2) + this.ToString();
            }


            public static Card? FromString(string value)
            {
                value = value.ToLower();
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
                        if (type == CardType.WildDrawFour || type == CardType.Wild) color = CardColor.Black;
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

        public enum CardColor
        {
            Red, Blue, Green, Yellow, Black,
        }

        public enum CardType
        {
            Zero, One, Two, Three, Four, Five, Six, Seven, Eight, Nine,
            Skip, Reverse, DrawTwo, WildDrawFour, Wild
        }




        public override void Construct(ulong channelId, ulong[] playerIds, DiscordShardedClient client, LoggingService logger, StorageService storage)
        {
            base.Construct(channelId, new ulong[] { }, client, logger, storage);

            // Make deck
            for (int color = 0; color < 4; color++)
            {
                for (int type = 0; type < 15; type++)
                {
                    var card = new Card((CardType)type, (CardColor)color);
                    deck.Add(card);
                    if (type > 0 && type < 13) deck.Add(card); // Second batch of colors excluding zero
                }
            }

            Bot.Random.Shuffle(deck);

            foreach (ulong id in playerIds) AddPlayer(id);

            while (playerCards[0].All(x => !CanPlace(x))) // First player can't play, try again
            {
                var index = Bot.Random.Next(deck.Count);
                var temp = deck[index];
                deck[index] = deck[0];
                deck[0] = temp;
            }

            if (deck[0].Type == CardType.Reverse) reversed = true;
            else if (deck[0].Type == CardType.Skip) Turn = FollowingPlayer();
        }



        public bool IsInput(string value)
        {
            return players.Count >= 2 && Card.FromString(StripPrefix(value)) != null;
        }


        public void DoTurn(string input)
        {
            var card = Card.FromString(StripPrefix(input)).Value;
            if (!playerCards[(int)Turn].Contains(card))
            {
                Message = $"Oops, you don't have that card!";
                return;
            }
            if (!CanPlace(card))
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

            if (User(Turn).IsBot && !AllBots) Message += $"• {User(Turn)?.Username} plays {input}!\n";
            else Message = "";

            playerCards[(int)Turn].Remove(card);
            deck.Insert(0, card);
            Time++;

            if (playerCards[(int)Turn].Count == 0)
            {
                State = State.Completed;
                Winner = Turn;
                Message = User(Winner).Id == client.CurrentUser.Id ? Bot.Random.Choose(WinTexts) : "";
                return;
            }

            SendCards(Turn);

            if (card.Type == CardType.Reverse)
            {
                reversed = !reversed;
                Message += $"• Now going {(reversed ? "backwards" : "forwards")}!\n";
            }

            Turn = FollowingPlayer();
            bool sentCards = false;

            if (card.Type == CardType.Skip)
            {
                Message += $"• {User(Turn)?.Username} skips their turn!\n";
                Turn = FollowingPlayer();
            }
            else if (card.Type == CardType.WildDrawFour || card.Type == CardType.DrawTwo)
            {
                int amount = card.Type == CardType.WildDrawFour ? 4 : 2;
                playerCards[(int)Turn].AddRange(deck.PopRange(Math.Min(deck.Count - 1, amount)));
                Message += $"• {User(Turn)?.Username} draws {amount} cards!\n";
            }

            while (playerCards[(int)Turn].All(x => !CanPlace(x))) // Next player can't place
            {
                int amount = 0;
                while (!CanPlace(playerCards[(int)Turn].Last()) && deck.Count > 1) // Give him cards until he can place
                {
                    amount++;
                    playerCards[(int)Turn].Add(deck.Pop());
                }

                if (amount > 0)
                {
                    if (!sentCards) SendCards(Turn);
                    sentCards = true;
                    Message += $"• {User(Turn)?.Username} couldn't move and had to draw {amount} card{"s".If(amount > 1)}{" and skip their turn!".If(!CanPlace(playerCards[(int)Turn].Last()))}!\n";
                }
                else Message += $"• {User(Turn)?.Username} couldn't move and had to skip their turn!\n";

                if (!CanPlace(playerCards[(int)Turn].Last()))
                {
                    Turn = FollowingPlayer(); // Deck ran out edge case, skip players until someone can place
                    sentCards = false;
                }
            }

            if (!sentCards) SendCards(Turn);

            if (!saved && !User(Turn).IsBot)
            {
                storage.StoreGame(this);
            }
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

            DoTurn(choice.ToString());
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
            description.Append($"```\n{deck[0].ToStringBig()}\n");

            if (State == State.Active) description.Append($"ᅠ\n*Say the name of a card to place it. Cards are sent in DMs.*\n*Use **{prefix}uno join** to join the game.*\n*Use **{prefix}uno leave** to abandon.*");


            return new EmbedBuilder()
            {
                Title = Winner == Player.None ? $"{(reversed ? "🔼" : "🔽")} {User(Turn)?.Username}'s turn" : $"🎉 {User(Turn)?.Username} is the winner! 🎉",
                Description = description.ToString(),
                Color = Card.RgbColor[(int)deck[0].Color],
                ThumbnailUrl = User(Turn)?.GetAvatarUrl(),
            };
        }


        public override string GetContent(bool showHelp = true)
        {
            return Message;
        }




        private Player FollowingPlayer()
        {
            return reversed ? PreviousPlayer() : NextPlayer();
        }


        private bool CanPlace(Card card)
        {
            return card.Color == CardColor.Black || deck[0].Color == CardColor.Black || deck[0].Color == card.Color || deck[0].Type == card.Type;
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
                Color = Time == 0 ? new Color?() : Card.RgbColor[(int)deck[0].Color],
            };

            embed.AddField("Your cards", cardList);
            if (Time > 0) embed.AddField("Top of the pile", deck[0]);

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
            if (deck.Count < 8) return "There aren't enough cards to take!";
            if (players.Contains(id)) return "You're already playing!";

            players.Add(id);
            playerCards.Add(deck.PopRange(Math.Min(deck.Count - 1, CardsPerPlayer)));
            SendCards(playerCards.Count - 1);

            return null;
        }


        public void RemovePlayer(ulong id) => RemovePlayer(players.IndexOf(id));
        public void RemovePlayer(int index)
        {
            players.RemoveAt(index);
            deck.AddRange(playerCards[index]);
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
