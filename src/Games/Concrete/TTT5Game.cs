using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using Discord;
using Discord.WebSocket;
using PacManBot.Utils;
using PacManBot.Constants;
using PacManBot.Extensions;

namespace PacManBot.Games.Concrete
{
    public class TTT5Game : MultiplayerGame, IMessagesGame
    {
        public override int GameIndex => 5;
        public override string GameName => "5-Tic-Tac-Toe";
        public override TimeSpan Expiry => TimeSpan.FromMinutes(60);


        private Board<Player> board;
        private readonly List<Pos> highlighted = new List<Pos>();
        private readonly int[] threes = { -1, -1 };


        private TTT5Game() { }

        protected override void Initialize(ulong channelId, SocketUser[] players, IServiceProvider services)
        {
            base.Initialize(channelId, players, services);

            board = new Player[5, 5];
            board.Fill(Player.None);
        }



        public bool IsInput(string value, ulong userId)
        {
            return userId == User(Turn)?.Id && Regex.IsMatch(StripPrefix(value).ToUpper(), @"^[ABCDE][12345]$");
        }


        public void Input(string input, ulong userId = 1)
        {
            input = StripPrefix(input).ToUpper();
            int x = input[0] - 'A';
            int y = input[1] - '1';

            if (State != State.Active || board[x, y] != Player.None) return; // Cell is already occupied

            board[x, y] = Turn;
            Time++;
            LastPlayed = DateTime.Now;

            if ((Winner = FindWinner()) == Player.None)
            {
                Turn = Turn.Opponent;
            }
            else
            {
                State = State.Completed;
                Turn = Winner;
            }
        }


        public override EmbedBuilder GetEmbed(bool showHelp = true)
        {
            if (State == State.Cancelled) return CancelledEmbed();

            var description = new StringBuilder();

            for (int i = 0; i < UserId.Length; i++)
            {
                if (i == Turn) description.Append("►");
                description.Append($"{((Player)i).Symbol()} {$"{threes[i]} lines".If(threes[i] >= 0)} - " +
                                   $"{User(i).NameandDisc().SanitizeMarkdown()}\n");
            }

            description.Append("ᅠ\n");

            for (int y = Winner == Player.None ? -1 : 0; y < board.Height; y++)
            {
                for (int x = Winner == Player.None ? -1 : 0; x < board.Width; x++)
                {
                    if (y < 0 && x < 0) description.Append(CustomEmoji.Empty);
                    else if (y < 0) description.Append(FullColumn(x) ? CustomEmoji.Empty : CustomEmoji.LetterCircle[x]);
                    else if (x < 0) description.Append(FullRow(y) ? CustomEmoji.Empty : CustomEmoji.NumberCircle[y + 1]);
                    else description.Append(board[x, y].Symbol(highlighted.Contains((x, y))) ?? Player.None.Circle());
                }
                description.Append('\n');
            }

            if (State == State.Active)
            {
                description.Append($"ᅠ\nSay a column and row to place an {(Turn == Player.Red ? "X" : "O")} in that cell (Example: B4)");
                description.Append("\nTo win you must make **more lines of three** than your opponent,\n" +
                                   "but if someone makes a line of **four**, they **win instantly**!");
            }


            return new EmbedBuilder()
            {
                Title = ColorEmbedTitle(),
                Description = description.ToString(),
                Color = Turn.Color,
                ThumbnailUrl = Winner == Player.None ? Turn.Symbol().ToEmote()?.Url : User(Winner)?.GetAvatarUrl(),
            };
        }


        private Player FindWinner()
        {
            if (Time < 7) return Player.None;
            if (board.FindLines(Turn, 4, highlighted)) return Turn;
            if (Time < board.Length) return Player.None;

            for (Player i = 0; i < 2; i++)
            {
                var lines = new List<Pos>();
                board.FindLines(i, 3, lines);
                threes[i] = lines.Count / 3;
                highlighted.AddRange(lines);
            }

            return threes[Player.Red] > threes[Player.Blue] ? Player.Red
                 : threes[Player.Red] < threes[Player.Blue] ? Player.Blue
                 : Player.Tie;
        }


        public override void BotInput()
        {
            var moves = TryCompleteLines(Turn, 4) ?? TryCompleteLines(Turn.Opponent, 4) ?? // Win or avoid losing
                        TryCompleteFlyingLines(Turn) ?? TryCompleteFlyingLines(Turn.Opponent); // Forced win / forced lose situations

            if (Time < 2 && board[2, 2] == Player.None && Bot.Random.Next(4) > 0) moves = new List<Pos> { (2, 2) };

            if (moves == null) // Lines of 3
            {
                var lines = TryCompleteLines(Turn, 3);
                var blocks = TryCompleteLines(Turn.Opponent, 3);

                if (lines == null && blocks == null)
                {
                    moves = TryCompleteLines(Turn, 2) ?? EmptyCells(board); // Next to itself 
                }
                else
                {
                    var combo = new List<Pos>();
                    if (lines != null)
                    {
                        combo.AddRange(lines.GroupBy(x => x).Where(g => g.Count() > 1).Select(g => g.Key)); // Double line
                    }
                    if (blocks != null)
                    {
                        combo.AddRange(blocks.GroupBy(x => x).Where(g => g.Count() > 1).Select(g => g.Key)); // Double block
                    }
                    if (lines != null && blocks != null)
                    {
                        combo.AddRange(lines.Where(x => blocks.Contains(x))); // line + block
                    }

                    if (combo.Count > 0) moves = combo;
                    else moves = lines ?? blocks;
                }
            }

            Pos choice = Bot.Random.Choose(moves);
            Input($"{(char)('A' + choice.x)}{1 + choice.y}");
        }


        private List<Pos> TryCompleteLines(Player player, int length)
        {
            uint count = 0;
            List<Pos> matches = new List<Pos>();
            Pos? missing = null;


            void CheckCell(int x, int y)
            {
                Pos pos = (x, y);

                if (board[pos] == player) count++; // Consecutive symbols
                else if (board[pos] == Player.None) // Find a gap
                {
                    if (missing != null) count = 0; // There was already a gap, line is broken
                    missing = pos;
                }
                else // line is broken
                {
                    count = 0;
                    missing = null;
                }

                if (count == length - 1 && missing.HasValue) matches.Add(missing.Value);
            }


            for (int y = 0; y < board.Height; y++) // Rows
            {
                for (int x = 0; x < board.Width; x++)
                {
                    CheckCell(x, y);
                }
                count = 0;
                missing = null;
            }

            for (int x = 0; x < board.Width; x++) // Columns
            {
                for (int y = 0; y < board.Height; y++)
                {
                    CheckCell(x, y);
                }
                count = 0;
                missing = null;
            }

            for (int d = length - 1; d <= board.Height + board.Width - length; d++) // Top-to-left diagonals
            {
                for (int x, y = 0; y <= d; y++)
                {
                    if (y < board.Height && (x = d - y) < board.Width)
                    {
                        CheckCell(x, y);
                    }
                }
                count = 0;
                missing = null;
            }

            for (int d = length - 1; d <= board.Height + board.Width - length; d++) // Top-to-right diagonals
            {
                for (int x, y = 0; y <= d; y++)
                {
                    if (y < board.Height && (x = board.Width - 1 - d + y) >= 0)
                    {
                        CheckCell(x, y);
                    }
                }
                count = 0;
                missing = null;
            }

            return matches.Count > 0 ? matches : null;
        }


        private List<Pos> TryCompleteFlyingLines(Player player) // A flying line is when there is a line of 3 in the center with the borders empty
        {
            uint count;
            Pos? missing;
            var matches = new List<Pos>();


            void CheckCell(int x, int y)
            {
                Pos pos = (x, y);
                if (board[pos] == player) count++; // Consecutive symbols
                else if (board[pos] == Player.None) missing = pos; // Find a gap

                if (count == 2 && missing.HasValue) matches.Add(missing.Value);
            }


            for (int y = 0; y < board.Height; y++) // Rows
            {
                count = 0;
                missing = null;
                if (board[0, y] != Player.None || board[board.Width - 1, y] != Player.None) continue;

                for (int x = 1; x < board.Width - 1; x++) CheckCell(x, y);
            }

            for (int x = 0; x < board.Width; x++) // Columns
            {
                count = 0;
                missing = null;
                if (board[x, 0] != Player.None || board[x, board.Height - 1] != Player.None) continue;

                for (int y = 1; y < board.Height - 1; y++) CheckCell(x, y);
            }

            if (board[0, 0] == Player.None && board[board.Width - 1, board.Height - 1] == Player.None)
            {
                count = 0;
                missing = null;

                for (int d = 1; d < board.Width - 1; d++) CheckCell(d, d);
            }

            if (board[board.Width - 1, 0] == Player.None && board[0, board.Height - 1] == Player.None)
            {
                count = 0;
                missing = null;

                for (int d = 1; d < board.Width - 1; d++) CheckCell(board.Width - 1 - d, d);
            }
            
            return matches.Count > 0 ? matches : null;
        }


        private bool FullColumn(int x)
        {
            return new Range(board.Height).All(y => board[x, y] != Player.None);
        }


        private bool FullRow(int y)
        {
            return new Range(board.Width).All(x => board[x, y] != Player.None);
        }


        private static List<Pos> EmptyCells(Board<Player> board)
        {
            return board.Positions.Where(p => board[p] == Player.None).ToList();
        }
    }
}
