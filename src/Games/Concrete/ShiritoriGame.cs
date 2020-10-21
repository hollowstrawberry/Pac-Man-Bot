using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.Exceptions;
using PacManBot.Constants;
using PacManBot.Extensions;
using PacManBot.Services;
using PacManBot.Utils;

namespace PacManBot.Games
{
    public class ShiritoriGame : MultiplayerGame, IMessagesGame
    {
        // Constants

        public override int GameIndex => 11;
        public override string GameName => "Shiritori";
        public override TimeSpan Expiry => TimeSpan.FromMinutes(15);
        public string FilenameKey => "uno";

        public static Regex Alphabet = new Regex(@"^[a-z]+$");
        public static string SmallLetters = "ᵃᵇᶜᵈᵉᶠᵍʰᶦʲᵏˡᵐⁿᵒᵖᵠʳˢᵗᵘᵛʷˣʸᶻ";

        public List<DiscordUser> Players { get; private set; }
        public TimeSpan TimeLimit { get; private set; }
        public int VisualTimeRemaining { get; set; }

        public override ulong[] UserId => Players.Select(x => x.Id).ToArray();
        public override ValueTask<bool> IsBotTurnAsync() => new ValueTask<bool>(_botTurn);

        private WordService _wordService;
        private readonly List<string> _pastWords = new List<string>();
        private string _message = "";
        private bool _botTurn;


        private ShiritoriGame() { }

        protected override async Task InitializeAsync(ulong channelId, DiscordUser[] players, IServiceProvider services)
        {
            Players = players.ToList();
            await base.InitializeAsync(channelId, players, services);
            _wordService = services.Get<WordService>();
            Turn = 0;
            State = GameState.Active;
            TimeLimit = TimeSpan.FromSeconds(8);
            VisualTimeRemaining = TimeLimit.Seconds;
            _botTurn = true;
        }

        public bool IsInput(string value, ulong userId)
        {
            return Alphabet.IsMatch(value.Trim().ToLowerInvariant())
                && Turn >= 0 && Turn < UserId.Length && UserId[Turn] == userId;
        }

        public Task InputAsync(string input, ulong userId = 1)
        {
            input = input.Trim().ToLowerInvariant();

            if (_pastWords.Contains(input))
            {
                _pastWords.Add(input);
                State = GameState.Win;
                if (Players.Count == 1) Winner = Player.None;
                else if (Turn == 0) Winner = Players.Count - 1;
                else Winner = Turn - 1;
                _message = "You used a word that had already been used!";
                return Task.CompletedTask;
            }

            if (!_wordService.Words.Contains(input.ToLowerInvariant()))
            {
                _message = $"\"{input}\" is not a recognized word!";
                return Task.CompletedTask;
            }

            VisualTimeRemaining = TimeLimit.Seconds;
            _pastWords.Add(input);
            _message = "";
            LastPlayed = DateTime.Now;
            if (Turn == Players.Count - 1) Turn = 0;
            else Turn += 1;
            if (Players.Count == 1) _botTurn = !_botTurn;
            return Task.CompletedTask;
        }

        public override Task BotInputAsync()
        {
            if (_pastWords.Count == 0)
            {
                _pastWords.Add(Program.Random.Choose(_wordService.Words).ToLowerInvariant());
            }
            else
            {
                string pick;
                do { pick = Program.Random.Choose(_wordService.Words).ToLowerInvariant(); }
                while (pick[0] != _pastWords.Last().Last() || _pastWords.Contains(pick));
                _pastWords.Add(pick);
            }
            _botTurn = false;
            return Task.CompletedTask;
        }

        public override ValueTask<string> GetContentAsync(bool showHelp = true)
        {
            if (State == GameState.Active)
                return new ValueTask<string>(
                    $"⏰ **{VisualTimeRemaining}**\n{Players[Turn].Mention}'s turn\n{_message}");
            else if (State == GameState.Cancelled)
                return new ValueTask<string>("");
            else
                return new ValueTask<string>(_message);
        }

        public override ValueTask<DiscordEmbedBuilder> GetEmbedAsync(bool showHelp = true)
        {
            if (State == GameState.Cancelled) return new ValueTask<DiscordEmbedBuilder>(CancelledEmbed());

            var words = _pastWords.TakeLast(6).ToList();
            var desc = words.Select((x, i) => i == words.Count - 1
                ? x.Slice(0, -1) + $"**{x.Last()}**"
                : ToSmallLetters(x)).JoinString("\n");

            var embed = new DiscordEmbedBuilder()
            {
                Title = GameName,
                Color = State == GameState.Active ? Turn.Color : Winner.Color,
                Description = desc,
            };

            if (State != GameState.Active)
            {
                int rounds = _pastWords.Count / Math.Max(2, Players.Count);
                embed.AddField(Empty, $"{Players[Turn].Mention} lost the game!\nThe game lasted {rounds} rounds");
            }

            return new ValueTask<DiscordEmbedBuilder>(embed);
        }

        public static string ToSmallLetters(string str)
        {
            if (!Alphabet.IsMatch(str)) throw new ArgumentException("Argument must be a full lowercase word");

            return str.Select(x => SmallLetters[x - 'a']).JoinString();
        }
    }
}
