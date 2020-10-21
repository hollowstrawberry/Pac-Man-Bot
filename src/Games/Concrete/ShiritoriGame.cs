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

        public static Regex Alphabet = new Regex(@"^[A-Z]+$");
        public static string SmallLetters = "ᴀʙᴄᴅᴇꜰɢʜɪᴊᴋʟᴍɴᴏᴘǫʀsᴛᴜᴠᴡxʏᴢ";

        public override ulong[] UserId => _players.Select(x => x.Id).ToArray();

        public TimeSpan TimeLimit { get; private set; }
        public int VisualTimeRemaining { get; set; }

        private WordService _wordService;
        private List<string> _pastWords = new List<string>();
        private List<DiscordUser> _players;
        private string _message = "";


        private ShiritoriGame() { }

        protected override async Task InitializeAsync(ulong channelId, DiscordUser[] players, IServiceProvider services)
        {
            await base.InitializeAsync(channelId, players, services);
            _wordService = services.Get<WordService>();
            Turn = 0;
            _players = players.ToList();
            TimeLimit = TimeSpan.FromSeconds(5);
        }

        public bool IsInput(string value, ulong userId)
        {
            return Alphabet.IsMatch(value.Trim().ToLowerInvariant())
                && Turn >= 0 && Turn < UserId.Length && UserId[Turn] == userId;
        }

        public Task InputAsync(string input, ulong userId = 1)
        {
            input = input.Trim().ToUpperInvariant();

            if (_pastWords.Contains(input))
            {
                _pastWords.Add(input);
                State = GameState.Win;
                if (_players.Count == 1) Winner = Player.None;
                else Winner = Turn;
                _message = "You used a word that had already been used!";
                return Task.CompletedTask;
            }

            if (!_wordService.Words.Contains(input))
            {
                _message = $"\"{input}\" is not a recognized word!";
                return Task.CompletedTask;
            }

            _pastWords.Add(input);
            _message = "";
            LastPlayed = DateTime.Now;
            if (Turn == _players.Count - 1) Turn = 0;
            else Turn += 1;
            return Task.CompletedTask;
        }

        public override Task BotInputAsync()
        {
            if (_pastWords.Count == 0)
            {
                _pastWords.Add(Program.Random.Choose(_wordService.Words));
            }
            else
            {
                string pick;
                do { pick = Program.Random.Choose(_wordService.Words); }
                while (pick[0] != _pastWords.Last().Last());
                _pastWords.Add(pick);
            }
            return Task.CompletedTask;
        }

        public override ValueTask<string> GetContentAsync(bool showHelp = true)
        {
            if (State == GameState.Active)
                return new ValueTask<string>($"⏰{VisualTimeRemaining}{CustomEmoji.Empty.If(_message != null)}{_message}");
            else
                return new ValueTask<string>(_message);
        }

        public override ValueTask<DiscordEmbedBuilder> GetEmbedAsync(bool showHelp = true)
        {
            var embed = new DiscordEmbedBuilder()
            {
                Title = GameName,
                Color = Turn.Color
            };


            return new ValueTask<DiscordEmbedBuilder>(embed);
        }

        public static string ToSmallLetters(string str)
        {
            if (!Alphabet.IsMatch(str)) throw new ArgumentException("Argument must be a full uppercase word");

            return str.Select(x => SmallLetters[x - 'A']).JoinString();
        }
    }
}
