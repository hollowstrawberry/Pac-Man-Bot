using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using Discord;
using Discord.WebSocket;
using PacManBot.Constants;
using PacManBot.Services;
using static PacManBot.Games.GameUtils;

namespace PacManBot.Games
{
    public class C4Game : TwoPlayerGame, IMessagesGame
    {
        private const int Columns = 7, Rows = 6;
        private static readonly TimeSpan _expiry = TimeSpan.FromMinutes(5);

        private Player[,] board;
        private List<Pos> highlighted;

        public override string Name => "Connect Four";
        public override TimeSpan Expiry => _expiry;


        public C4Game(ulong channelId, ulong[] userId, DiscordShardedClient client, LoggingService logger, StorageService storage)
            : base(channelId, userId, client, logger, storage)
        {
            highlighted = new List<Pos>();
            board = new Player[Columns, Rows];
            for (int x = 0; x < Columns; x++)
            {
                for (int y = 0; y < Rows; y++)
                {
                    board[x, y] = Player.None;
                }
            }
        }



        public bool IsInput(string value)
        {
            return int.TryParse(StripPrefix(value), out int num) && num > 0 && num <= Columns;
        }


        public void DoTurn(string input)
        {
            if (State != State.Active) return;
            LastPlayed = DateTime.Now;

            int column = int.Parse(StripPrefix(input)) - 1;
            if (!AvailableColumns(board).Contains(column)) return; // Column is full

            PlacePiece(board, column, turn);

            Time++;

            if (FindWinner(board, turn, Time, highlighted)) winner = turn;
            else if (IsTie(board, turn, Time)) winner = Player.Tie;

            if (winner == Player.None)
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

            for (int i = 0; i < 2; i++)
            {
                description.Append($"{"►".If(i == (int)turn)}{((Player)i).Circle()} - {User((Player)i).NameandNum().SanitizeMarkdown()}\n");
            }

            description.Append("ᅠ\n");

            if (State == State.Active)
            {
                var columns = AvailableColumns(board);
                for (int x = 0; x < Columns; x++) description.Append(columns.Contains(x) ? CustomEmoji.NumberCircle[x + 1] : CustomEmoji.Empty);
                description.Append('\n');
            }

            for (int y = 0; y < Rows; y++)
            {
                for (int x = 0; x < Columns; x++)
                {
                    description.Append(board[x, y].Circle(highlighted.Contains(new Pos(x, y))));
                }
                description.Append('\n');
            }

            if (State == State.Active) description.Append($"ᅠ\n*Say the number of a column (1 to 7) to place a piece*");


            return new EmbedBuilder()
            {
                Title = EmbedTitle(),
                Description = description.ToString(),
                Color = turn.Color(),
                ThumbnailUrl = winner == Player.None ? turn.Circle().ToEmote()?.Url : User(winner)?.GetAvatarUrl(),
            };
        }


        private static bool FindWinner(Player[,] board, Player player, int time, List<Pos> highlighted = null)
        {
            if (time < 7) return false;
            return board.FindLines(player, 4, highlighted);
        }


        private static bool IsTie(Player[,] board, Player turn, int time)
        {
            if (time < Rows*Columns - 6) return false;
            else if (time == Rows*Columns) return true;

            turn = turn.OtherPlayer();

            foreach (int column in AvailableColumns(board)) // Checks that all possible configurations result in a tie
            {
                var tempBoard = (Player[,])board.Clone();
                PlacePiece(tempBoard, column, turn);
                if (FindWinner(tempBoard, turn, time + 1) || !IsTie(tempBoard, turn, time + 1)) return false;
            }

            return true;
        }



        public override void DoTurnAI()
        {
            var moves = new Dictionary<int, int>(); // Column and amount of possible loses by playing in that column
            var avoidMoves = new List<int>(); // Moves where it can lose right away

            foreach (int column in AvailableColumns(board)) // All moves it can make
            {
                var tempBoard = (Player[,])board.Clone();
                PlacePiece(tempBoard, column, turn);

                if (FindWinner(tempBoard, turn, Time + 1)) // Can win in 1 move
                {
                    moves = new Dictionary<int, int> { { column, 0 } };
                    avoidMoves = new List<int>();
                    break;
                }

                moves.Add(column, MayLoseCount(tempBoard, turn, turn.OtherPlayer(), Time + 1, depth: 3));

                if (MayLoseCount(tempBoard, turn, turn.OtherPlayer(), Time + 1, depth: 1) > 0) avoidMoves.Add(column); // Can lose right away
            }

            if (avoidMoves.Count < moves.Count)
            {
                foreach (int move in avoidMoves) moves.Remove(move);
            }

            int leastLoses = moves.Min(x => x.Value);
            var finalOptions = moves.Where(x => x.Value == leastLoses).Select(x => x.Key).ToList();

            DoTurn($"{1 + GlobalRandom.Choose(finalOptions)}");
        }


        private static int MayLoseCount(Player[,] board, Player player, Player turn, int time, int depth)
        {
            int count = 0;

            if (depth <= 0 || time == board.LengthX() * board.LengthY()) return count;

            foreach (int column in AvailableColumns(board))
            {
                var tempBoard = (Player[,])board.Clone();
                PlacePiece(tempBoard, column, turn);

                if (turn != player && FindWinner(tempBoard, turn, time + 1)) // Loses to opponent
                {
                    count++;
                }
                else if (turn != player || !FindWinner(tempBoard, turn, time + 1)) // Isn't a win
                {
                    count += MayLoseCount(tempBoard, player, turn.OtherPlayer(), time + 1, depth - 1);
                }
            }

            return count;
        }


        private static void PlacePiece(Player[,] board, int column, Player player)
        {
            for (int row = Rows - 1; row >= 0; row--)
            {
                if (board[column, row] == Player.None)
                {
                    board[column, row] = player;
                    break;
                }
            }
        }
        

        private static List<int> AvailableColumns(Player[,] board)
        {
            List<int> available = new List<int>();
            for (int x = 0; x < Columns; x++)
            {
                bool full = true;
                for (int y = 0; y < Rows; y++)
                {
                    if (board[x, y] == Player.None) full = false;
                }
                if (!full) available.Add(x);
            }
            return available;
        }
    }
}
