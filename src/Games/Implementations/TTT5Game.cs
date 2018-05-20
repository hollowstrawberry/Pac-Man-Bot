using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using Discord;
using Discord.WebSocket;
using PacManBot.Constants;
using PacManBot.Services;
using static PacManBot.Games.GameUtils;

namespace PacManBot.Games
{
    public class TTT5Game : TwoPlayerGame, IMessagesGame
    {
        private static readonly TimeSpan _expiry = TimeSpan.FromMinutes(5);

        private Player[,] board;
        private List<Pos> highlighted = new List<Pos>();
        private int[] threes = new int[] { -1, -1 };

        public override string Name => "5-Tic-Tac-Toe";
        public override TimeSpan Expiry => _expiry;



        public TTT5Game(ulong channelId, ulong[] userId, DiscordShardedClient client, LoggingService logger, StorageService storage)
            : base(channelId, userId, client, logger, storage)
        {
            board = new Player[5, 5];
            for (int x = 0; x < 5; x++)
            {
                for (int y = 0; y < 5; y++)
                {
                    board[x, y] = Player.None;
                }
            }
        }



        public bool IsInput(string value)
        {
            return Regex.IsMatch(StripPrefix(value).ToUpper(), @"^[ABCDE][12345]$");
        }


        public void DoTurn(string input)
        {
            input = StripPrefix(input).ToUpper();
            int x = input[0] - 'A';
            int y = input[1] - '1';

            if (State != State.Active || board[x, y] != Player.None) return; // Cell is already occupied

            board[x, y] = turn;
            Time++;
            LastPlayed = DateTime.Now;

            if ((winner = FindWinner()) == Player.None)
            {
                turn = turn.OtherPlayer();
            }
            else
            {
                State = State.Completed;
                turn = winner;
            }
        }


        public override EmbedBuilder GetEmbed(bool showHelp = true)
        {
            if (State == State.Cancelled) return CancelledEmbed();

            var description = new StringBuilder();

            for (int i = 0; i < UserId.Length; i++)
            {
                description.Append($"{"►".If(i == (int)turn)}{((Player)i).Symbol()} {$"{threes[i]} lines".If(threes[i] >= 0)} - {User((Player)i).NameandNum().SanitizeMarkdown()}\n");
            }

            description.Append("ᅠ\n");

            for (int y = winner == Player.None ? -1 : 0; y < board.LengthY(); y++)
            {
                for (int x = winner == Player.None ? -1 : 0; x < board.LengthX(); x++)
                {
                    if (y < 0 && x < 0) description.Append(CustomEmoji.Empty);
                    else if (y < 0) description.Append(FullColumn(x) ? CustomEmoji.Empty : CustomEmoji.LetterCircle[x]);
                    else if (x < 0) description.Append(FullRow(y) ? CustomEmoji.Empty : CustomEmoji.NumberCircle[y + 1]);
                    else description.Append(board[x, y].Symbol(highlighted.Contains(new Pos(x, y))) ?? Player.None.Circle());
                }
                description.Append('\n');
            }

            if (State == State.Active)
            {
                description.Append($"ᅠ\nSay a column and row to place an {(turn == Player.Red ? "X" : "O")} in that cell (Example: B4)");
                description.Append("\nTo win you must make **more lines of three** than your opponent,\nbut if someone makes a line of **four**, they **win instantly**!");
            }


            return new EmbedBuilder()
            {
                Title = EmbedTitle(),
                Description = description.ToString(),
                Color = turn.Color(),
                ThumbnailUrl = winner == Player.None ? turn.Symbol().ToEmote()?.Url : User(winner)?.GetAvatarUrl(),
            };
        }


        private Player FindWinner()
        {
            if (Time < 7) return Player.None;
            if (board.FindLines(turn, 4, highlighted)) return turn;

            if (Time < board.Length) return Player.None;
            else // Game over, count threees
            {
                for (int i = 0; i < 2; i++)
                {
                    var lines = new List<Pos>();
                    board.FindLines((Player)i, 3, lines);
                    threes[i] = lines.Count / 3;
                    highlighted.AddRange(lines);
                }

                return threes[0] > threes[1] ? Player.Red : threes[0] < threes[1] ? Player.Blue : Player.Tie;
            }
        }


        public override void DoTurnAI()
        {
            string debug = "priority";
            var moves = TryCompleteLine(turn, 4) ?? TryCompleteLine(turn.OtherPlayer(), 4) ?? // Win or avoid losing
                        TryCompleteFlyingLine(turn) ?? TryCompleteFlyingLine(turn.OtherPlayer()); // Forced win / forced lose situations

            if (Time < 2 && board[2, 2] == Player.None && GlobalRandom.Next(4) > 0) moves = new List<Pos> { new Pos(2, 2) };

            if (moves == null) // Lines of 3
            {
                var lines = TryCompleteLine(turn, 3);
                var blocks = TryCompleteLine(turn.OtherPlayer(), 3);

                if (lines == null && blocks == null)
                {
                    moves = TryCompleteLine(turn, 2) ?? EmptyCells(board); // Next to itself 
                    debug = "random";
                }
                else
                {
                    var combo = new List<Pos>();
                    if (lines != null) combo.AddRange(lines.GroupBy(x => x).Where(g => g.Count() > 1).Select(g => g.Key)); // Double line
                    if (blocks != null) combo.AddRange(blocks.GroupBy(x => x).Where(g => g.Count() > 1).Select(g => g.Key)); // Double block
                    if (lines != null && blocks != null) combo.AddRange(lines.Where(x => blocks.Contains(x))); // line + block

                    if (combo.Count > 0)
                    {
                        moves = combo;
                        debug = "combo";
                    }
                    else
                    {
                        moves = lines ?? blocks;
                        debug = "threes";
                    }
                }
            }

            Pos choice = GlobalRandom.Choose(moves);
            DoTurn($"{(char)('A' + choice.x)}{1 + choice.y}");

            debug += $" {choice.x+1},{choice.y+1} /";
            foreach (var m in moves) debug += $" {m.x+1},{m.y+1}";
            logger.Log(LogSeverity.Debug, "AI", debug);

        }


        private List<Pos> TryCompleteLine(Player player, int length)
        {
            uint count = 0;
            List<Pos> matches = new List<Pos>();
            Pos missing = null;


            void CheckCell(Pos pos)
            {
                if (board.At(pos) == player) count++; // Consecutive symbols
                else if (board.At(pos) == Player.None) // Find a gap
                {
                    if (missing != null) count = 0; // There was already a gap, line is broken
                    missing = pos;
                }
                else // line is broken
                {
                    count = 0;
                    missing = null;
                }

                if (count == length - 1 && missing != null) matches.Add(missing);
            }


            for (int y = 0; y < board.LengthY(); y++) // Rows
            {
                for (int x = 0; x < board.LengthX(); x++)
                {
                    CheckCell(new Pos(x, y));
                }
                count = 0;
                missing = null;
            }

            for (int x = 0; x < board.LengthX(); x++) // Columns
            {
                for (int y = 0; y < board.LengthY(); y++)
                {
                    CheckCell(new Pos(x, y));
                }
                count = 0;
                missing = null;
            }

            for (int d = length - 1; d <= board.LengthY() + board.LengthX() - length; d++) //Top-to-left diagonals
            {
                for (int x, y = 0; y <= d; y++)
                {
                    if (y < board.LengthY() && (x = d - y) < board.LengthX())
                    {
                        CheckCell(new Pos(x, y));
                    }
                }
                count = 0;
                missing = null;
            }

            for (int d = length - 1; d <= board.LengthY() + board.LengthX() - length; d++) //Top-to-right diagonals
            {
                for (int x, y = 0; y <= d; y++)
                {
                    if (y < board.LengthY() && (x = board.LengthX() - 1 - d + y) >= 0)
                    {
                        CheckCell(new Pos(x, y));
                    }
                }
                count = 0;
                missing = null;
            }

            return matches.Count > 0 ? matches : null;
        }


        private List<Pos> TryCompleteFlyingLine(Player player) // A flying line is when there is a line of 3 in the center with the extremes empty
        {
            uint count = 0;
            List<Pos> matches = new List<Pos>();
            Pos missing = null;


            void CheckCell(Pos pos)
            {
                if (board.At(pos) == player) count++; // Consecutive symbols
                else if (board.At(pos) == Player.None) missing = pos; // Find a gap

                if (count == 2 && missing != null) matches.Add(missing);
            }


            for (int y = 0; y < board.LengthY(); y++) // Rows
            {
                count = 0;
                missing = null;
                if (board[0, y] != Player.None || board[board.LengthX() - 1, y] != Player.None) continue;

                for (int x = 1; x < board.LengthX() - 1; x++) CheckCell(new Pos(x, y));
            }

            for (int x = 0; x < board.LengthX(); x++) // Columns
            {
                count = 0;
                missing = null;
                if (board[x, 0] != Player.None || board[x, board.LengthY() - 1] != Player.None) continue;

                for (int y = 1; y < board.LengthY() - 1; y++) CheckCell(new Pos(x, y));
            }

            if (board[0, 0] == Player.None && board[board.LengthX() - 1, board.LengthY() - 1] == Player.None)
            {
                count = 0;
                missing = null;

                for (int d = 1; d < board.LengthX() - 1; d++) CheckCell(new Pos(d, d));
            }

            if (board[board.LengthX() - 1, 0] == Player.None && board[0, board.LengthY() - 1] == Player.None)
            {
                count = 0;
                missing = null;

                for (int d = 1; d < board.LengthX() - 1; d++) CheckCell(new Pos(board.LengthX() - 1 - d, d));
            }
            
            return matches.Count > 0 ? matches : null;
        }


        private bool FullColumn(int x)
        {
            bool full = true;
            for (int y = 0; y < board.LengthY(); y++)
            {
                if (board[x, y] == Player.None) full = false;
            }
            return full;
        }

        private bool FullRow(int y)
        {
            bool full = true;
            for (int x = 0; x < board.LengthX(); x++)
            {
                if (board[x, y] == Player.None) full = false;
            }
            return full;
        }


        private static List<Pos> EmptyCells(Player[,] board)
        {
            List<Pos> empty = new List<Pos>();
            for (int y = 0; y < board.LengthY(); y++)
            {
                for (int x = 0; x < board.LengthX(); x++)
                {
                    if (board[x, y] == Player.None) empty.Add(new Pos(x, y));
                }
            }
            return empty;
        }
    }
}
