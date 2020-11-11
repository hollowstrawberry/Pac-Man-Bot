using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using PacManBot.Constants;
using PacManBot.Extensions;
using Range = PacManBot.Utils.Range;

namespace PacManBot.Games.Concrete
{
    public class C4Game : MultiplayerGame, IMessagesGame
    {
        public override int GameIndex => 11;
        public override string GameName => "Connect Four";
        public override TimeSpan Expiry => TimeSpan.FromMinutes(60);

        private const int Columns = 7, Rows = 6;


        private Board<Player> board;
        private List<Pos> highlighted;


        private C4Game() { }

        protected override Task InitializeAsync(ulong channelId, DiscordUser[] players, IServiceProvider services)
        {
            base.InitializeAsync(channelId, players, services);

            highlighted = new List<Pos>();
            board = new Player[Columns, Rows];
            board.Fill(Player.None);

            return Task.CompletedTask;
        }



        public bool IsInput(string value, ulong userId)
        {
            return userId == UserId[Turn] && int.TryParse(StripPrefix(value), out int num) && num > 0 && num <= Columns;
        }


        public Task InputAsync(string input, ulong userId = 1)
        {
            if (State != GameState.Active) return Task.CompletedTask;
            LastPlayed = DateTime.Now;

            int column = int.Parse(StripPrefix(input)) - 1;
            if (!AvailableColumns(board).Contains(column)) return Task.CompletedTask; // Column is full

            DropPiece(board, column, Turn);

            Time++;

            if (FindWinner(board, Turn, Time, highlighted)) Winner = Turn;
            else if (IsTie(board, Turn, Time)) Winner = Player.Tie;

            if (Winner == Player.None)
            {
                Turn = Turn.Opponent;
            }
            else
            {
                State = GameState.Completed;
                Turn = Winner;
            }

            return Task.CompletedTask;
        }


        public override async ValueTask<DiscordEmbedBuilder> GetEmbedAsync(bool showHelp = true)
        {
            if (State == GameState.Cancelled) return CancelledEmbed();

            var description = new StringBuilder();

            for (int i = 0; i < 2; i++)
            {
                if (i == Turn) description.Append('►');
                description.Append($"{((Player)i).Circle()} - {(await GetUserAsync(i)).Mention}\n");
            }

            description.Append($"{Empty}\n");

            if (State == GameState.Active)
            {
                var columns = AvailableColumns(board);
                for (int x = 0; x < Columns; x++) description.Append(columns.Contains(x) ? CustomEmoji.NumberCircle[x + 1] : CustomEmoji.Empty);
                description.Append('\n');
            }

            for (int y = 0; y < Rows; y++)
            {
                for (int x = 0; x < Columns; x++)
                {
                    description.Append(board[x, y].Circle(highlighted.Contains((x, y))));
                }
                description.Append('\n');
            }

            if (State == GameState.Active) description.Append($"{Empty}\n*Say the number of a column (1 to 7) to drop a piece*");

            return new DiscordEmbedBuilder()
                .WithTitle(ColorEmbedTitle())
                .WithDescription(description.ToString())
                .WithColor(Turn.Color)
                .WithThumbnail(Winner < 0 ? "" : (await GetUserAsync(Winner))?.GetAvatarUrl(ImageFormat.Auto));
        }


        private static bool FindWinner(Board<Player> board, Player player, int time, List<Pos> highlighted = null)
        {
            if (time < 7) return false;
            return board.FindLines(player, 4, highlighted);
        }


        private static bool IsTie(Board<Player> board, Player turn, int time)
        {
            if (time < Rows*Columns - 6) return false;
            else if (time == Rows*Columns) return true;

            turn = turn.Opponent;

            foreach (int column in AvailableColumns(board)) // Checks that all possible configurations result in a tie
            {
                var tempBoard = board.Copy();
                DropPiece(tempBoard, column, turn);
                if (FindWinner(tempBoard, turn, time + 1) || !IsTie(tempBoard, turn, time + 1)) return false;
            }

            return true;
        }



        public override Task BotInputAsync()
        {
            var moves = new Dictionary<int, int>(); // Column and amount of possible loses by playing in that column
            var avoidMoves = new List<int>(); // Moves where it can lose right away

            var columns = AvailableColumns(board);
            if (columns.Count == 1)
            {
                moves.Add(columns[0], 0);
            }
            else
            {
                foreach (int column in columns) // All moves it can make
                {
                    var tempBoard = board.Copy();
                    DropPiece(tempBoard, column, Turn);

                    if (FindWinner(tempBoard, Turn, Time + 1)) // Can win in 1 move
                    {
                        moves = new Dictionary<int, int> { { column, 0 } };
                        avoidMoves = new List<int>();
                        break;
                    }

                    moves.Add(column, MayLoseCount(tempBoard, Turn, Turn.Opponent, Time + 1, depth: 3));

                    if (MayLoseCount(tempBoard, Turn, Turn.Opponent, Time + 1, depth: 1) > 0) avoidMoves.Add(column); // Can lose right away
                }
            }

            if (avoidMoves.Count < moves.Count)
            {
                foreach (int move in avoidMoves) moves.Remove(move);
            }

            int leastLoses = moves.Min(x => x.Value);
            var finalOptions = moves.Where(x => x.Value == leastLoses).Select(x => x.Key).ToList();

            return InputAsync($"{1 + Program.Random.Choose(finalOptions)}");
        }




        private static int MayLoseCount(Board<Player> board, Player player, Player turn, int time, int depth)
        {
            int count = 0;

            if (depth <= 0 || time == board.Length) return count;

            foreach (int column in AvailableColumns(board))
            {
                var tempBoard = board.Copy();
                DropPiece(tempBoard, column, turn);

                if (turn != player && FindWinner(tempBoard, turn, time + 1)) // Loses to opponent
                {
                    count++;
                }
                else if (turn != player || !FindWinner(tempBoard, turn, time + 1)) // Isn't a win
                {
                    count += MayLoseCount(tempBoard, player, turn.Opponent, time + 1, depth - 1);
                }
            }

            return count;
        }



        private static void DropPiece(Board<Player> board, int column, Player player)
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
        

        private static List<int> AvailableColumns(Board<Player> board)
        {
            return new Range(Columns).Where(x => new Range(Rows).Any(y => board[x, y] == Player.None)).ToList();
        }
    }
}
