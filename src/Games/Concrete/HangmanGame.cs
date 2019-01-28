using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Discord;
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
        public override TimeSpan Expiry => TimeSpan.FromMinutes(60);

        private string word;
        private char[] progress;
        private int mistakes;
        private List<char> wrongChars = new List<char>();
        private ulong winnerId;


        /// <summary>Sets the word in a new game.</summary>
        public void SetWord(string word)
        {
            this.word = word.ToUpperInvariant();
            progress = word.Select(x => x == ' ' ? ' ' : '_').ToArray();
        }


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



        public bool IsInput(string value, ulong userId)
        {
            value = StripPrefix(value.ToUpperInvariant());

            if (word == null
                || userId == OwnerId
                || value.Length > 1 && !ValidFullGuess(value)
                || !Alphabet.IsMatch(value))
                return false;
            return true;
        }


        public void Input(string input, ulong userId = 1)
        {
            input = StripPrefix(input.ToUpperInvariant());

            if (input.Length == 1)
            {
                char ch = input[0];

                if (progress.Contains(ch) || wrongChars.Contains(ch)) return;

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
                        State = State.Lose;
                        progress = word.ToArray();
                    }
                }
                else if (!progress.Contains('_'))
                {
                    State = State.Win;
                    winnerId = userId;
                }
            }
            else
            {
                if (word == input)
                {
                    progress = word.ToArray();
                    State = State.Win;
                    winnerId = userId;
                }
                else if (++mistakes == DeadAmount)
                {
                    State = State.Lose;
                    progress = word.ToArray();
                }
            }
        }



        public override EmbedBuilder GetEmbed(bool showHelp = true)
        {
            if (State == State.Cancelled) return CancelledEmbed();

            var embed = new EmbedBuilder();

            if (OwnerId != 1) embed.Title = $"{Owner.Username}'s {GameName}";
            else embed.Title = GameName;

            if (State == State.Lose)
            {
                embed.Title = $"💀 {embed.Title} 💀";
                embed.Color = Colors.Red;
            }
            else if (State == State.Win)
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
            embed.ThumbnailUrl = config.Content.hangmanStageImages[stage];


            string displayWord = progress.Select(x => x == ' ' ? "\n" : $"`{x}`").JoinString(' ');
            
            if (State == State.Active)
            {
                var missed = wrongChars // vowels in order then consonants in order
                    .GroupBy(c => "AEIOU".Contains(c))
                    .OrderBy(x => !x.Key)
                    .Select(x => x.OrderBy(c => c))
                    .SelectMany(c => c)
                    .ToList();

                embed.Fields = new List<EmbedFieldBuilder>
                {
                    new EmbedFieldBuilder
                    {
                        Name = missed.Count == 0 ? "ᅠ" : "_Missed_",
                        IsInline = true,
                        Value = missed.Count == 0 ? "ᅠ"
                            : missed.Split(5).Select(x => x.JoinString(' ')).JoinString('\n'),
                    },
                    new EmbedFieldBuilder
                    {
                        Name = "ᅠ",
                        IsInline = true,
                        Value = displayWord,
                    },
                    new EmbedFieldBuilder
                    {
                        Name = "ᅠ",
                        IsInline = false,
                        Value = $"_Guess a letter or the full {(word.Contains(' ') ? "phrase" : "word")}!_",
                    },
                };
            }
            else
            {
                embed.Description = displayWord;
                if (State == State.Win)
                {
                    embed.Description += $"\n\n{client.GetUser(winnerId)?.Mention ?? "Someone"} guessed it!";
                }
            }

            return embed;
        }




        private bool ValidFullGuess(string value)
        {
            if (value.Length != word.Length) return false;

            for (int i = 0; i < word.Length; ++i)
            {
                if (progress[i] != '_' && progress[i] != value[i]) return false;
            }

            return true;
        }
    }
}
