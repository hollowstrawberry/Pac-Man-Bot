using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.Exceptions;
using PacManBot.Constants;
using PacManBot.Extensions;
using PacManBot.Utils;

namespace PacManBot.Games.Concrete
{
    [DataContract]
    public class UnoGame : MultiplayerGame, IMessagesGame, IStoreableGame
    {
        // Constants

        public override int GameIndex => 10;
        public override string GameName => "Uno";
        public override TimeSpan Expiry => TimeSpan.FromHours(48);
        public string FilenameKey => "uno";

        private const int CardsPerPlayer = 7;

        private static readonly Card Wild = new Card(CardType.Wild, CardColor.Black);
        private static readonly Card WildDrawFour = new Card(CardType.WildDrawFour, CardColor.Black);

        private static readonly DiscordColor[] RgbCardColor = {
            Colors.Red, Colors.Blue, Colors.Green, Colors.Yellow, Colors.DarkBlack,
        };
        private static readonly string[] CardColorEmote = {
            CustomEmoji.RedSquare, CustomEmoji.BlueSquare, CustomEmoji.GreenSquare, CustomEmoji.YellowSquare, CustomEmoji.BlackSquare,
        };
        private static readonly string[] CardTypeEmote = CustomEmoji.NumberCircle.Concatenate(new[] {
            CustomEmoji.UnoSkip, CustomEmoji.UnoReverse, CustomEmoji.AddTwo, CustomEmoji.UnoWild, CustomEmoji.AddFour
        });


        // Fields

        [DataMember] private readonly List<UnoPlayer> players = new List<UnoPlayer>();
        [DataMember] private List<Card> drawPile = new List<Card>();
        [DataMember] private List<Card> discardPile = new List<Card>();
        [DataMember] private bool reversed;
        [IgnoreDataMember] private readonly List<string> gameLog = new List<string>();
        [IgnoreDataMember] private List<UnoPlayer> updatedPlayers = new List<UnoPlayer>();


        // Properties

        [DataMember] public override GameState State { get; set; }
        [DataMember] public override DateTime LastPlayed { get; set; }
        [DataMember] public override int Time { get; set; }
        [DataMember] public override ulong ChannelId { get; set; }
        [DataMember] public override ulong MessageId { get; set; } = 1;
        [DataMember] public override Player Turn { get; protected set; }
        [DataMember] public override Player Winner { get; protected set; }

        private Card TopCard { get => discardPile[0]; set => discardPile[0] = value; }
        private UnoPlayer CurrentPlayer => players[Turn];
        private Player FollowingTurn => reversed ? PreviousPlayer() : NextPlayer();
        private Player PrecedingTurn => reversed ? NextPlayer() : PreviousPlayer();

        public override async ValueTask<bool> IsBotTurnAsync()
            => players.Count > 1 && State == GameState.Active && await CurrentPlayer.IsBotAsync();

        public override ulong[] UserId
        {
            get => players?.Select(x => x.id).ToArray();
            set => throw new InvalidOperationException($"Use {nameof(TryAddPlayerAsync)} and {nameof(RemovePlayerAsync)} instead");
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

            public UnoGame game;
            public DiscordMessage message;
            private DiscordUser user;


            public async ValueTask<DiscordUser> GetUserAsync()
            {
                if (user != null) return user;
                return user = game.Guild == null ? await game.Client.GetUserAsync(id) : await game.Guild.GetMemberAsync(id);
            }

            public async ValueTask<bool> IsBotAsync() => (await GetUserAsync()).IsBot;
            public async ValueTask<string> MentionAsync() => (await GetUserAsync())?.Mention;

            public override string ToString() => GetUserAsync().GetAwaiter().GetResult().DisplayName();


            private UnoPlayer() { } // Used in serialization

            public UnoPlayer(DiscordUser user, UnoGame game)
            {
                id = user.Id;
                this.user = user;
                this.game = game;
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

                return WildType
                    ? typeStr + $" {Color}".If(Color != CardColor.Black)
                    : $"{Color}{typeStr}";
            }

            public string ToStringBig()
            {
                var sb = new StringBuilder();
                for (int y = 0; y < 5; y++)
                {
                    sb.Append($"{CustomEmoji.Empty}".Repeat(2));
                    for (int x = 0; x < 3; x++)
                    {
                        if (x == 1 && y == 2) sb.Append(CardTypeEmote[(int)Type]);
                        else sb.Append(CardColorEmote[(int)Color]);
                    }
                    sb.Append('\n');
                }
                sb.Append($"{CustomEmoji.Empty}".Repeat(2));
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
                value = value.ReplaceMany((" ", ""), ("auto", ""), ("uno", ""), ("draw2", "drawtwo"), ("draw4", "drawfour"));

                CardColor? color = EnumTraits<CardColor>.Values
                    .FirstOrNull(x => value.StartsOrEndsWith(x.ToString().ToLowerInvariant()));

                CardType? type = EnumTraits<CardType>.Values.ToList().Reversed()
                    .FirstOrNull(x => value.StartsOrEndsWith(x.ToString().ToLowerInvariant())
                                 || x <= CardType.Nine && value.StartsOrEndsWith(((int)x).ToString()));

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
        }




        // Game methods

        private UnoGame() { }

        protected override async Task InitializeAsync(ulong channelId, DiscordUser[] players, IServiceProvider services)
        {
            await base.InitializeAsync(channelId, null, services);

            // Make deck
            foreach (var color in EnumTraits<CardColor>.Values.Take(4))
            {
                foreach (var type in EnumTraits<CardType>.Values)
                {
                    bool wild = Card.IsWild(type);
                    var card = new Card(type, wild ? CardColor.Black : color);
                    drawPile.Add(card);
                    if (type > CardType.Zero && !wild) drawPile.Add(card); // Second batch of colors except zero
                }
            }

            Program.Random.Shuffle(drawPile);
            discardPile = new List<Card> { drawPile.Pop() };

            while (TopCard.CardsToDraw > 0 || TopCard.Type == CardType.Skip) // Invalid first cards
            {
                drawPile.Add(discardPile.Pop());
                drawPile.Swap(drawPile.Count - 1, Program.Random.Next(drawPile.Count - 1));
                discardPile.Add(drawPile.Pop());
            }


            players = players.Distinct().Take(10).ToArray();
            var toInvite = players.Skip(1).Where(x => !x.IsBot);
            var toAdd = players.Where(x => !toInvite.Contains(x));

            if (toInvite.Any())
            {
                var mentions = toInvite.Select(x => x.Mention);
                string inviteMsg = $"{string.Join(", ", mentions)} You've been invited to play Uno. " +
                                   $"Type `{Storage.GetPrefix(Channel)}uno join` to join.\n";
                Message = inviteMsg;
            }

            foreach (var player in toAdd) await TryAddPlayerAsync(player);

            ApplyCardEffect();

            if (Turn < 0 || Turn >= players.Length) Turn = 0; // There's an error I don't know how is caused or how to fix

            await Games.SaveAsync(this);
        }



        public bool IsInput(string value, ulong userId)
        {
            if (players.Count < 2) return false;

            value = StripPrefix(value.ToLowerInvariant());

            if (UserId.Contains(userId))
            {
                if (value == "cards" || value == "uno" || value == "callout") return true;

                if (userId == (CurrentPlayer.GetUserAsync().GetAwaiter().GetResult()).Id)
                {
                    if (IsWaitingForColor()) return Enum.TryParse<CardColor>(value, true, out _); // Wild color
                    else return value == "draw" || value == "skip" || value.Contains("auto") || Card.Parse(value, this).HasValue;
                }
            }

            return false;
        }


        public async Task InputAsync(string input, ulong userId = 1)
        {
            LastPlayed = DateTime.Now;
            input = StripPrefix(input.ToLowerInvariant());
            bool calledByAi = await CurrentPlayer.IsBotAsync();


            // Send cards
            if (input == "cards")
            {
                var player = players.First(x => x.id == userId);
                player.message = null;
                await SendCardsAsync(player);
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
                    await ClearGameLogAsync();
                    Callout(forgot);
                }
                await Games.SaveAsync(this);
                return;
            }

            // Set wild color (non-bots)
            else if (IsWaitingForColor())
            {
                var color = Enum.Parse<CardColor>(input, true);
                TopCard = new Card(TopCard.Type, color);
                gameLog.Add($"• {CurrentPlayer} picked {color}!");
                ApplyCardEffect();
            }

            // Drawing a card
            else if (input == "draw" || input == "skip" || input == "auto" && CurrentPlayer.cards.All(x => !CanDiscard(x)))
            {
                await ClearGameLogAsync();

                UnoState previousUno = CurrentPlayer.uno;
                CancelCallouts();
                Draw(CurrentPlayer, 1);

                var drawn = CurrentPlayer.cards.Last();
                if (CanDiscard(drawn))
                {
                    if (await CurrentPlayer.IsBotAsync() && drawn.Color == CardColor.Black)
                    {
                        drawn = new Card(drawn.Type, HighestColor(CurrentPlayer.cards));
                    }

                    CurrentPlayer.uno = previousUno;
                    gameLog.Add($"• {CurrentPlayer} drew and placed {drawn}");
                    CurrentPlayer.cards.Pop();
                    Discard(drawn);
                    ApplyCardEffect();
                }
                else
                {
                    updatedPlayers.Add(CurrentPlayer);
                    gameLog.Add($"• {CurrentPlayer} drew a card and skipped a turn.");
                    Turn = FollowingTurn;
                }
            }

            // Checking and playing a card
            else
            {
                await ClearGameLogAsync();

                Card card;
                var tryCard = Card.Parse(input, this);

                if (tryCard.HasValue) card = tryCard.Value;
                else
                {
                    if (input.Contains("auto")) gameLog.Add("Oops, \"auto\" found no valid matches.");
                    else throw new FormatException($"Unexpected invalid card \"{input}\" from {(await CurrentPlayer.GetUserAsync()).DebugName()} in {Channel.DebugName()}");
                    return;
                }

                if (!CurrentPlayer.cards.Contains(card.NormalizeColor()))
                {
                    gameLog.Add("Oops, you don't have that card!");
                    return;
                }
                else if (!CanDiscard(card))
                {
                    gameLog.Add("Oops, that card doesn't match the type or color!");
                    return;
                }

                gameLog.Add($"• {CurrentPlayer} plays {card}");

                if (!await CurrentPlayer.IsBotAsync()) updatedPlayers.Add(CurrentPlayer);

                CurrentPlayer.cards.Remove(card.NormalizeColor());
                Discard(card);

                CancelCallouts();

                if (CurrentPlayer.cards.Count == 1)
                {
                    CurrentPlayer.uno = input.Contains("uno") ? UnoState.Said : UnoState.Forgot;
                }
                else if (CurrentPlayer.cards.Count == 0)
                {
                    State = GameState.Completed;
                    Winner = Turn;
                    if (CurrentPlayer.id == Client.CurrentUser.Id) gameLog.Add($"\n {Program.Random.Choose(Content.gameWinTexts)}");
                    return;
                }

                ApplyCardEffect();
            }

            if (IsWaitingForColor())
            {
                Message = $"{await CurrentPlayer.MentionAsync()} choose a color: red/blue/green/yellow";
            }
            else if (Guild != null)
            {
                Message = $"Your turn, {await CurrentPlayer.MentionAsync()}";
            }


            updatedPlayers.Add(CurrentPlayer);

            if (!calledByAi || !await CurrentPlayer.IsBotAsync())
            {
                await Games.SaveAsync(this);
                foreach (var player in updatedPlayers.Distinct().Where(x => !x.IsBotAsync().GetAwaiter().GetResult()).ToArray())
                {
                    await SendCardsAsync(player);
                }
                updatedPlayers = new List<UnoPlayer>();
            }
        }


        public override async Task BotInputAsync()
        {
            string input = "draw";
            var playable = CurrentPlayer.cards.Where(CanDiscard).ToList();

            if (playable.Count > 0)
            {
                Card choice;

                var niceCards = playable.Where(x => x.Type >= CardType.Skip).ToList();
                if (players[FollowingTurn].cards.Count <= 2 && niceCards.Count > 0) choice = Program.Random.Choose(niceCards);
                else
                {
                    var noWilds = playable.Where(x => x.Color != CardColor.Black).ToList();
                    choice = Program.Random.Choose(noWilds.Count > 0 ? noWilds : playable); // Leaves wilds for last
                }

                if (choice.Color == CardColor.Black && CurrentPlayer.cards.Count > 1)
                {
                    choice = new Card(choice.Type, HighestColor(CurrentPlayer.cards));
                }

                input = choice.ToString();
                if (CurrentPlayer.cards.Count == 2 && !Program.Random.OneIn(10)) input += "uno"; // Sometimes "forgets" to say uno
            }

            var forgot = players.FirstOrDefault(x => x.uno == UnoState.Forgot);
            if (forgot != null && (await forgot.IsBotAsync() || !Program.Random.OneIn(3))) // Sometimes calls out
            {
                Callout(forgot);
            }

            await InputAsync(input);
        }

        private static CardColor HighestColor(List<Card> cards)
        {
            var groups = cards.GroupBy(c => c.Color).Where(x => x.Key != CardColor.Black);
            if (!groups.Any()) return Program.Random.Choose(EnumTraits<CardColor>.Values);

            int max = groups.Select(x => x.Count()).Max();
            return Program.Random.Choose(groups.Where(x => x.Count() == max).Select(x => x.Key).ToList());
        }



        public override async ValueTask<DiscordEmbedBuilder> GetEmbedAsync(bool showHelp = true)
        {
            if (State == GameState.Cancelled) return CancelledEmbed();

            var description = new StringBuilder();
            string prefix = Storage.GetPrefix(Channel);

            if (players.Count < 2) description.Append("👥 You need at least 2 players.\nWait for friends to join or invite some bots!\n\n");

            description.Append("```diff\n");
            for (int i = 0; i < players.Count; i++)
            {
                string pre = i == Turn ? "+ " : Winner != Player.None ? "- " : "";
                description.Append($"{pre}{players[i]}");
                if (players[i].cards.Count > 0) description.Append($" - {players[i].cards.Count}🃏{" Uno!".If(players[i].uno == UnoState.Said)}");
                description.Append('\n');
            }
            description.Append($"\n```\n{TopCard.ToStringBig()}\n");

            if (State == GameState.Active)
            {
                description.Append(
                    $"{Empty}\nSay the name of a card to discard it or \"draw\" to draw another.\n" +
                    $"Your cards are shown in a DM, say \"cards\" to resend.\n".If(Guild != null) +
                    $"Use **{prefix}uno join** to join the game.\n".If(Guild != null) +
                    $"Use **{prefix}uno help** for rules and more commands.");
            }


            var embed = new DiscordEmbedBuilder()
                .WithTitle(Winner == Player.None
                    ? $"{(reversed ? "🔼" : "🔽")} {CurrentPlayer}'s turn"
                    : $"🎉 {CurrentPlayer} is the winner! 🎉")
                .WithDescription(description.ToString().Truncate(2047))
                .WithColor(RgbCardColor[(int)TopCard.Color])
                .WithThumbnail((await CurrentPlayer.GetUserAsync()).GetAvatarUrl(ImageFormat.Auto));

            if (Guild == null) embed.AddField("Your cards", CardsDisplay(players[0]));

            return embed;
        }


        public override ValueTask<string> GetContentAsync(bool showHelp = true)
        {
            if (State == GameState.Cancelled) return new ValueTask<string>("");

            return new ValueTask<string>($"{gameLog.JoinString("\n")}\n{Message}".TruncateStart(2000));
        }




        private bool IsWaitingForColor()
        {
            return Time > 0 && TopCard.Color == CardColor.Black;
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
                    Program.Random.Shuffle(discardPile);
                    drawPile = discardPile.ToList();
                    discardPile = new List<Card> { drawPile.Pop() };
                    gameLog.Add("• Shuffled and turned over the discard pile.");

                    if (drawPile.Count == 0) break; // No more cards aaaaaa!
                }

                player.cards.Add(drawPile.Pop().NormalizeColor());
                amount--;
            }
        }


        private void ApplyCardEffect()
        {
            var card = TopCard;

            if (IsWaitingForColor()) return;


            switch (card.Type)
            {
                case CardType.Skip:
                case CardType.Reverse when players.Count == 2 && Time > 0:
                    Turn = FollowingTurn;
                    gameLog.Add($"• {CurrentPlayer} skips a turn!");
                    break;

                case CardType.Reverse:
                    reversed = !reversed;
                    gameLog.Add($"• Now going {(reversed ? "backwards" : "forwards")}!");
                    break;

                case CardType.DrawTwo:
                case CardType.WildDrawFour:
                    Turn = FollowingTurn;
                    Draw(CurrentPlayer, card.CardsToDraw);
                    updatedPlayers.Add(CurrentPlayer);
                    gameLog.Add($"• {CurrentPlayer} draws {card.CardsToDraw} cards and skips a turn!");
                    break;
            }

            if (Time > 0)
            {
                Turn = FollowingTurn;
            }
        }


        private async ValueTask<bool> ClearGameLogAsync()
        {
            bool clear = !await CurrentPlayer.IsBotAsync();
            if (clear) gameLog.Clear();
            Message = "";
            return clear;
        }


        private void Callout(UnoPlayer player)
        {
            Draw(player, 2);
            gameLog.Add($"• {player} was called out for not saying Uno and drew 2 cards!");
            updatedPlayers.Add(player);
        }


        private void CancelCallouts()
        {
            foreach (var player in players)
            {
                if (player.uno == UnoState.Forgot) player.uno = UnoState.NotCalledOut;
            }
        }



        private string CardsDisplay(UnoPlayer player)
        {
            string cards = player.cards
                .ToList().Sorted()
                .GroupBy(x => x.Color)
                .Select(group => $"{CardColorEmote[(int)group.Key]} {group.JoinString(", ")}")
                .JoinString("\n");

            return cards == "" ? "*None*" : cards;
        }


        private async Task SendCardsAsync(UnoPlayer player)
        {
            if (await player.GetUserAsync() == null || await player.IsBotAsync() || Guild == null) return;

            var embed = new DiscordEmbedBuilder
            {
                Title = $"{GameName}",
                Description = $"Send the name of a card in {Channel.Mention} to discard that card." +
                              $"\nIf you can't or don't want to choose any card, say \"draw\" instead." +
                              $"\nYou can also use \"auto\" instead of a color/number/both.\n{Empty}",
                Color = RgbCardColor[(int)TopCard.Color],
            };

            embed.AddField("Your cards", CardsDisplay(player).Truncate(1024), false);

            bool resend = false;

            if (player.message == null) resend = true;
            else
            {
                try { await player.message.ModifyAsync(embed: embed.Build()); }
                catch (NotFoundException) { resend = true; }
            }

            if (resend)
            {
                try
                {
                    player.message = await (await player.GetUserAsync() as DiscordMember).SendMessageAsync(embed: embed.Build());
                }
                catch (UnauthorizedException)
                {
                    gameLog.Add($"{await player.MentionAsync()} You can't play unless you have DMs enabled!\n");
                    await RemovePlayerAsync(player);
                } 
            }
        }



        /// <summary>Adds a new player to the game. Returns the fail reason if it can't be added.</summary>
        public async Task<string> TryAddPlayerAsync(DiscordUser user)
        {
            if (players.Count == 10) return "The game is full!";
            if (players.Any(x => x.id == user.Id)) return "You're already playing!";

            var player = new UnoPlayer(user, this);
            players.Add(player);
            Draw(player, CardsPerPlayer);

            await SendCardsAsync(player);

            return null;
        }


        /// <summary>Removes a user from the game, and cancels the game if it can't continue.</summary>
        public Task RemovePlayerAsync(DiscordUser user) => RemovePlayerAsync(players.First(x => x.id == user.Id));

        private async Task RemovePlayerAsync(UnoPlayer player)
        {
            drawPile.AddRange(player.cards);
            players.Remove(player);

            if (players.Count < 2)
            {
                State = GameState.Cancelled;
                return;
            }

            int xturns = 0;
            while (Turn >= players.Count || await CurrentPlayer.IsBotAsync())
            {
                Turn = FollowingTurn;

                if (++xturns > players.Count) // all bots
                {
                    State = GameState.Cancelled;
                    return;
                }
            }
        }




        public void PostDeserialize(IServiceProvider services)
        {
            SetServices(services);

            foreach (var player in players)
            {
                player.game = this;
            }
        }
    }
}
