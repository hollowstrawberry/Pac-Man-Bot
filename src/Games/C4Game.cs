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
    class C4Game : GameInstance
    {
        private const int Columns = 7, Rows = 6;
        private static readonly TimeSpan _expiry = TimeSpan.FromMinutes(2);

        private Player[,] board;
        private List<Pos> highlighted;

        public override string Name => "Connect Four";
        public override TimeSpan Expiry => _expiry;

        public bool PlayingAI => client.GetUser(userId[(int)turn]).IsBot;


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

            if (PlayingAI) DoTurnAI();
        }



        public override bool IsInput(string value)
        {
            return int.TryParse(StripPrefix(value), out int num) && num > 0 && num <= Columns;
        }


        public override void DoTurn(string rawInput)
        {
            base.DoTurn(rawInput);

            int column = int.Parse(StripPrefix(rawInput)) - 1;
            if (!AvailableColumns(board).Contains(column)) return; // Column is full

            PlacePiece(board, column, turn);

            time++;

            if (FindWinner(board, turn, highlighted)) winner = turn;
            else if (IsTie(board, turn, time)) winner = Player.Tie;

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

            for (int i = 0; i < 2; i++)
            {
                description.Append($"{"►".If(i == (int)turn)}{((Player)i).Circle()} - {User((Player)i).NameandNum().SanitizeMarkdown()}\n");
            }

            description.Append("ᅠ\n");

            if (state == State.Active)
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

            if (state == State.Active) description.Append($"ᅠ\n*Say the number of a column (1 to 7) to place a piece*");


            return new EmbedBuilder()
            {
                Title = EmbedTitle(),
                Description = description.ToString(),
                Color = turn.Color(),
                ThumbnailUrl = winner == Player.None ? turn.Circle().ToEmote()?.Url : User(winner)?.GetAvatarUrl(),
            };
        }


        private static bool FindWinner(Player[,] board, Player player, List<Pos> highlighted = null)
        {
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
                if (FindWinner(tempBoard, turn) || !IsTie(tempBoard, turn, time + 1)) return false;
            }

            return true;
        }



        private void DoTurnAI()
        {
            int? target = null;

            var losingMoves = new List<int>();

            foreach (int column in AvailableColumns(board)) // All moves it can make
            {
                var tempBoard = (Player[,])board.Clone();
                PlacePiece(tempBoard, column, turn);

                if (FindWinner(tempBoard, turn)) // Can win in 1 move
                {
                    target = column;
                    break;
                }

                bool canLose = false;
                foreach (int columnOpponent in AvailableColumns(tempBoard)) // All moves the opponent can follow with
                {
                    var tempBoardOpponent = (Player[,])tempBoard.Clone();
                    PlacePiece(tempBoardOpponent, columnOpponent, turn.OtherPlayer());

                    if (FindWinner(tempBoardOpponent, turn.OtherPlayer()))
                    {
                        canLose = true;
                        break;
                    }
                }

                if (canLose) losingMoves.Add(column);
            }

            if (target == null) // Random
            {
                foreach (int x in losingMoves)
                {
                    Console.Write($"{x+1} ");
                }
                Console.Write('\n');

                var columns = AvailableColumns(board);
                if (losingMoves.Count < columns.Count) columns.RemoveAll(x => losingMoves.Contains(x)); // Tries to avoid the possibility of losing
                target = GlobalRandom.Choose(columns);
            }

            DoTurn($"{1 + target}");
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
