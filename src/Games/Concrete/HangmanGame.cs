using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DSharpPlus.Entities;
using PacManBot.Constants;
using PacManBot.Extensions;

namespace PacManBot.Games.Concrete
{
    public class HangmanGame : ChannelGame, IMessagesGame
    {
        public static readonly Regex Alphabet = new Regex(@"^[a-zA-Z ]+$");
        private const int DeadAmount = 11;

        public override string GameName => "Hangman";
        public override int GameIndex => 13;
        public override TimeSpan Expiry => TimeSpan.FromHours(1);

        private string word;
        private char[] progress;
        private int mistakes;
        private List<char> wrongChars = new List<char>();
        private ulong winnerId;


        private HangmanGame() : base() { }

        /// <summary>Starts a hangman game with a random word.</summary>
        public HangmanGame(ulong channelId, IServiceProvider services)
            : base(channelId, new ulong[] { 1 }, services)
        {
            SetWord(Program.Random.Choose(config.Content.hangmanWords));
        }


        /// <summary>Creates a custom hangman game belonging to a user.
        /// The game will only start once the word is provided.</summary>
        public HangmanGame(ulong channelId, ulong ownerId, IServiceProvider services)
            : base(channelId, new[] { ownerId }, services)
        { }



        /// <summary>Sets the word in a new game.</summary>
        public void SetWord(string word)
        {
            this.word = word.ToUpperInvariant();
            progress = word.Select(x => x == ' ' ? ' ' : '_').ToArray();
        }



        public ValueTask<bool> IsInputAsync(string value, ulong userId)
        {
            value = StripPrefix(value.ToUpperInvariant());

            if (word == null
                || userId == OwnerId
                || value.Length > 1 && !ValidFullGuess(value)
                || !Alphabet.IsMatch(value)
                || value.Length == 1 && wrongChars.Contains(value[0])
                || value.Length == 1 && progress.Contains(value[0]))
                return new ValueTask<bool>(false);
            return new ValueTask<bool>(true);
        }


        public Task InputAsync(string input, ulong userId = 1)
        {
            input = StripPrefix(input.ToUpperInvariant());

            if (input.Length == 1)
            {
                char ch = input[0];

                if (progress.Contains(ch) || wrongChars.Contains(ch)) return Task.CompletedTask;

                int neat = 0;
                for (int i = 0; i < word.Length; ++i)
                {
                    if (word[i] == ch)
                    {
                        progress[i] = ch;
                        ++neat;
                    }
                }

                if (neat == 0)
                {
                    wrongChars.Add(ch);
                    if (++mistakes == DeadAmount)
                    {
                        State = GameState.Lose;
                        progress = word.ToArray();
                    }
                }
                else if (!progress.Contains('_'))
                {
                    State = GameState.Win;
                    winnerId = userId;
                }
            }
            else
            {
                if (word == input)
                {
                    progress = word.ToArray();
                    State = GameState.Win;
                    winnerId = userId;
                }
                else if (++mistakes == DeadAmount)
                {
                    State = GameState.Lose;
                    progress = word.ToArray();
                }
            }

            return Task.CompletedTask;
        }



        public override async ValueTask<DiscordEmbedBuilder> GetEmbedAsync(bool showHelp = true)
        {
            if (State == GameState.Cancelled) return CancelledEmbed();

            var embed = new DiscordEmbedBuilder();

            if (OwnerId != 1) embed.Title = $"{(await GetOwnerAsync())?.DisplayName()}'s {GameName}";
            else embed.Title = GameName;

            if (State == GameState.Lose)
            {
                embed.Title = $"💀 {embed.Title} 💀";
                embed.Color = Colors.Red;
            }
            else if (State == GameState.Win)
            {
                embed.Title = $"🎉 {embed.Title} 🎉";
                embed.Color = Colors.Green;
            }
            else
            {
                embed.Title = $"🚹 {embed.Title}";
                embed.Color = Colors.PacManYellow;
            }

            if (word == null)
            {
                embed.Description = $"Waiting for the secret phrase...";
                return embed;
            }

            int stage = mistakes < config.Content.hangmanStageImages.Length ? mistakes : config.Content.hangmanStageImages.Length - 1;
            embed.WithThumbnail(config.Content.hangmanStageImages[stage]);


            string displayWord = progress.Select(x => x == ' ' ? " " : $"`{x}`").JoinString(' ').Replace("   ", "\n");
            
            if (State == GameState.Active)
            {
                var missed = wrongChars // vowels in order then consonants in order
                    .GroupBy(c => "AEIOU".Contains(c))
                    .OrderBy(x => !x.Key)
                    .Select(x => x.OrderBy(c => c))
                    .SelectMany(c => c)
                    .ToList();

                embed.AddField(Empty, displayWord, true);
                embed.AddField(missed.Count == 0 ? Empty : "_Missed_",
                    missed.Count == 0 ? Empty : missed.Split(5).Select(x => x.JoinString(' ')).JoinString('\n'), true);
                embed.AddField(Empty, $"_Guess a letter or the full {(word.Contains(' ') ? "phrase" : "word")}!_", false);
            }
            else
            {
                embed.Description = displayWord;
                if (State == GameState.Win)
                {
                    var user = await Client.GetUserAsync(winnerId);
                    embed.Description += $"\n\n{user?.Mention ?? "Someone"} guessed it!";
                }
            }

            return embed;
        }




        private bool ValidFullGuess(string value)
        {
            if (value.Length != word.Length || value.ContainsAny(wrongChars)) return false;

            for (int i = 0; i < word.Length; ++i)
            {
                if (progress[i] != '_' && progress[i] != value[i]) return false;
            }

            return true;
        }
    }
}
