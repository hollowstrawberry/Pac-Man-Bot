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
    public class TTT5Game : GameInstance
    {
        private static readonly TimeSpan _expiry = TimeSpan.FromMinutes(5);

        private Player[,] board;
        private List<Pos> highlighted = new List<Pos>();
        private int[] threes = new int[] { -1, -1 };

        public override string Name => "Tic-Tac-Toe";
        public override TimeSpan Expiry => _expiry;

        public bool PlayingAI => client.GetUser(userId[(int)turn]).IsBot;



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

            if (PlayingAI) DoTurnAI();
        }



        public override bool IsInput(string value)
        {
            return Regex.IsMatch(value.ToUpper(), @"[ABCDE][12345]");
        }


        public override void DoTurn(string rawInput)
        {
            base.DoTurn(rawInput);
            rawInput = rawInput.ToUpper();

            int x = rawInput[0] - 'A';
            int y = rawInput[1] - '1';

            if (board[x, y] != Player.None) return; // Cell is already occupied

            board[x, y] = turn;
            time++;
            winner = FindWinner(board, turn, time, threes, highlighted);

            if (winner == Player.None)
            {
                turn = turn.OtherPlayer();
                if (PlayingAI) DoTurnAI();
            }
            else
            {
                state = State.Completed;
                turn = winner;
            }
        }


        public override EmbedBuilder GetEmbed(bool showHelp = true)
        {
            if (state == State.Cancelled) return CancelledEmbed();

            var description = new StringBuilder();

            for (int i = 0; i < userId.Length; i++)
            {
                description.Append($"{"►".If(i == (int)turn)}{((Player)i).Symbol()} {$"{threes[i]} lines".If(threes[i] >= 0)}" +
                    $" - {User((Player)i).NameandNum().SanitizeMarkdown()}\n");
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

            if (state == State.Active)
            {
                description.Append($"ᅠ\nSay a column and row to place an {(turn == Player.Red ? "X" : "O")} in that cell (Example: B4)");
                description.Append("\nTo win you must make **more lines of three** than your opponent.\nBut if someone makes a line of **four**, they win instantly!");
            }


            return new EmbedBuilder()
            {
                Title = EmbedTitle(),
                Description = description.ToString(),
                Color = turn.Color(),
                ThumbnailUrl = winner == Player.None ? turn.Symbol().ToEmote()?.Url : User(winner)?.GetAvatarUrl(),
            };
        }


        private static Player FindWinner(Player[,] board, Player turn, int time, int[] threes = null, List<Pos> highlighted = null)
        {
            if (time < 7) return Player.None;
            if (board.FindLines(turn, 4, highlighted)) return turn;

            if (time < board.Length) return Player.None;
            else // Game over, count threees
            {
                if (threes == null) threes = new int[] { 0, 0 };

                for (int i = 0; i < 2; i++)
                {
                    var lines = new List<Pos>();
                    board.FindLines((Player)i, 3, lines);
                    threes[i] = lines.Count / 3;
                    if (highlighted != null) highlighted.AddRange(lines);
                }

                return threes[0] > threes[1] ? Player.Red : threes[0] < threes[1] ? Player.Blue : Player.Tie;
            }
        }


        public override void DoTurnAI()
        {
            var moves = new Dictionary<Pos, int>(); // Cell and amount of possible loses by playing in that cell
            var avoidMoves = new List<Pos>(); // Moves where it can lose right away

            foreach (Pos pos in EmptyCells(board)) // All moves it can make
            {
                var tempBoard = (Player[,])board.Clone();
                tempBoard.SetAt(pos, turn);

                if (FindWinner(tempBoard, turn, time + 1) == turn) // Can win in 1 move
                {
                    moves = new Dictionary<Pos, int> { { pos, 0 } };
                    avoidMoves = new List<Pos>();
                    break;
                }

                moves.Add(pos, MayLoseCount(tempBoard, turn, turn.OtherPlayer(), time + 1, depth: 3));

                if (MayLoseCount(tempBoard, turn, turn.OtherPlayer(), time + 1, depth: 1) > 0) avoidMoves.Add(pos); // Can lose right away
            }

            if (avoidMoves.Count < moves.Count)
            {
                foreach (Pos move in avoidMoves) moves.Remove(move);
            }

            int leastLoses = moves.Min(x => x.Value);
            var finalOptions = moves.Where(x => x.Value == leastLoses).Select(x => x.Key).ToList();
            Pos target = GlobalRandom.Choose(finalOptions);

            DoTurn($"{(char)('A' + target.x)}{1 + target.y}");
        }


        private static int MayLoseCount(Player[,] board, Player player, Player turn, int time, int depth)
        {
            int count = 0;

            if (depth <= 0 || time == board.Length) return count;

            foreach (Pos pos in EmptyCells(board))
            {
                var tempBoard = (Player[,])board.Clone();
                tempBoard.SetAt(pos, turn);

                Player winner = FindWinner(tempBoard, turn, time + 1);
                if (winner == player.OtherPlayer()) // Loses to opponent
                {
                    count++;
                }
                else if (winner != player) // Isn't a win
                {
                    count += MayLoseCount(tempBoard, player, turn.OtherPlayer(), time + 1, depth - 1);
                }
            }

            return count;
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
