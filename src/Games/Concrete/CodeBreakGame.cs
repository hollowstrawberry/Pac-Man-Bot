using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using PacManBot.Constants;
using PacManBot.Extensions;
using Range = PacManBot.Utils.Range;

namespace PacManBot.Games.Concrete
{
    [DataContract]
    public class CodeBreakGame : ChannelGame, IMessagesGame, IStoreableGame
    {
        public static readonly Regex Numbers = new Regex(@"^[0-9]+$");
        public override string GameName => "Code Break";
        public override int GameIndex => 14;
        public override TimeSpan Expiry => TimeSpan.FromHours(24);
        public string FilenameKey => "code";

        [DataMember] public string Code { get; protected set; }
        [DataMember] private readonly List<string> guesses;

        [DataMember] public override int Time { get; set; }
        [DataMember] public override DateTime LastPlayed { get; set; }
        [DataMember] public override ulong OwnerId { get => base.OwnerId; protected set => base.OwnerId = value; }
        [DataMember] public override ulong ChannelId { get => base.ChannelId; set => base.ChannelId = value; }
        [DataMember] public override ulong MessageId { get => base.MessageId; set => base.MessageId = value; }


        private CodeBreakGame() : base() { }

        /// <summary>Starts a game with a random code with a set number of digits.</summary>
        public CodeBreakGame(ulong channelId, ulong userId, int length, IServiceProvider services)
            : base(channelId, new ulong[] { userId }, services)
        {
            if (length < 1 || length > 10) throw new ArgumentException($"Invalid code length {length}");

            var nums = new Range(10).ToList();
            var digits = new int[length];

            for (int i = 0; i < length; i++)
            {
                digits[i] = Program.Random.Choose(nums);
                nums.Remove(digits[i]);
            }

            Code = digits.JoinString();
            guesses = new List<string>();
        }

        public bool IsInput(string value, ulong userId)
        {
            value = StripPrefix(value);
            return value.Length == Code.Length && Numbers.IsMatch(value);
        }

        public async Task InputAsync(string input, ulong userId = 1)
        {
            input = StripPrefix(input);
            if (guesses.Count > 0 && guesses.Last() == null) guesses.Pop();
            if (input == Code) State = GameState.Win;
            if (input.Distinct().Count() < Code.Length) input = null; // can't contain the same digit twice
            guesses.Add(input);
            if (guesses.Count >= 99 && input != Code) State = GameState.Lose;
            await games.SaveAsync(this);
        }

        public override EmbedBuilder GetEmbed(bool showHelp = true)
        {
            if (State == GameState.Cancelled) return CancelledEmbed();

            var embed = new EmbedBuilder();

            if (State == GameState.Lose)
            {
                embed.Title += $"{GameName} - Failed";
                embed.Color = Colors.Red;
            }
            else if (State == GameState.Win)
            {
                embed.Title += $"🎉 {GameName} 🎉";
                embed.Color = Colors.Blue;
            }
            else
            {
                embed.Title = $"{GameName} 🔢";
                embed.Color = Colors.Blue;
            }

            var sb = new StringBuilder();

            if (guesses.Count == 0)
            {
                sb.Append($"**Send a guess to get clues. Example: {new Range(Code.Length).JoinString()}**\n");
            }
            else
            {
                int lines = Math.Min(20, 120 / Code.Length);
                foreach ((string guess, int index) in guesses.Select((x, i) => (x, i)).TakeLast(lines))
                {
                    if (guess == null)
                    {
                        sb.Append($"{CustomEmoji.Cross} Your guess can't contain the same digit twice!\n");
                    }
                    else
                    {
                        int match = guess.Where((c, i) => c == Code[i]).Count();
                        int near = guess.Where((c, i) => Code.Contains(c) && c != Code[i]).Count();
                        if (State == GameState.Win)
                        {
                            if (match == Code.Length) break;

                            sb.Append($"`{index + 1, 2}.` ");
                            sb.Append(guess.Select(x => CustomEmoji.Number[x - '0']).JoinString());
                            sb.Append($"{CustomEmoji.Empty}`{match}M` `{near}N`\n");
                        }
                        else
                        {
                            sb.Append($"`{index + 1, 2}.` ");
                            sb.Append(guess.Select(x => CustomEmoji.Number[x - '0']).JoinString());
                            sb.Append($"{CustomEmoji.Empty}Match: `{match}`{CustomEmoji.Empty}Near: `{near}`\n");
                        }
                    }
                }
            }

            embed.Description = sb.ToString().Trim();

            if (State == GameState.Active)
            {
                embed.AddField(Empty, $"_Find the secret {Code.Length}-digit code!_\n" +
                    $"_After each guess you will get a clue of of how many " +
                    $"of those digits match, and how many are near (present but in the wrong position)._", false);
            }
            else if (State == GameState.Win)
            {
                var wow = Math.Max(6, 7 + 2 * (Code.Length - 4));
                embed.AddField(Empty, $"\n`{guesses.Count, 2}.` {Code.Select(x => CustomEmoji.Number[x - '0']).JoinString()}" +
                    $"{CustomEmoji.Empty}`{Code.Length}M` `0N`" +
                    $"\n**Cracked the code in {guesses.Count} guesses!**{" ***Congratulations!***".If(guesses.Count <= wow)}", false);            
            }
            return embed;
        }

        public void PostDeserialize(IServiceProvider services)
        {
            SetServices(services);
        }
    }
}