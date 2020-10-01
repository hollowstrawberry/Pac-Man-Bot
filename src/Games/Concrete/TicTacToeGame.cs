using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using PacManBot.Constants;
using PacManBot.Extensions;

namespace PacManBot.Games.Concrete
{
    public class TicTacToeGame : MultiplayerGame, IMessagesGame
    {
        public override int GameIndex => 12;
        public override string GameName => "Tic-Tac-Toe";
        public override TimeSpan Expiry => TimeSpan.FromMinutes(30);

        private Board<Player> board;
        private List<Pos> highlighted;


        private TicTacToeGame() { }

        protected override Task InitializeAsync(ulong channelId, DiscordUser[] players, IServiceProvider services)
        {
            base.InitializeAsync(channelId, players, services);

            highlighted = new List<Pos>();
            board = new Player[3, 3];
            board.Fill(Player.None);

            return Task.CompletedTask;
        }



        public ValueTask<bool> IsInputAsync(string value, ulong userId)
        {
            return new ValueTask<bool>(
                userId == UserId[Turn] && int.TryParse(StripPrefix(value), out int num) && num > 0 && num <= board.Length);
        }


        public Task InputAsync(string input, ulong userId = 1)
        {
            int cell = int.Parse(StripPrefix(input)) - 1;
            int y = cell / board.Width;
            int x = cell % board.Width;

            if (State != GameState.Active || board[x, y] != Player.None) return Task.CompletedTask;

            board[x, y] = Turn;
            Time++;
            LastPlayed = DateTime.Now;

            if (FindWinner(board, Turn, highlighted)) Winner = Turn;
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

            for (int i = 0; i < UserId.Length; i++)
            {
                description.Append($"{"►".If(i == Turn)}{((Player)i).Symbol()} - {(await GetUserAsync(i)).Mention}\n");
            }

            description.Append($"{Empty}\n");

            for (int y = 0; y < board.Height; y++)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    description.Append(board[x, y].Symbol(highlighted.Contains((x, y))) ??
                        (State == GameState.Active ? $"{CustomEmoji.NumberCircle[1 + board.Width*y + x]}" : Player.None.Circle()));
                }
                description.Append('\n');
            }

            if (State == GameState.Active) description.Append($"{Empty}\n*Say the number of a cell (1 to 9) to place an {(Turn == Player.Red ? "X" : "O")}*");

            return new DiscordEmbedBuilder()
                .WithTitle(ColorEmbedTitle())
                .WithDescription(description.ToString())
                .WithColor(Turn.Color)
                .WithThumbnail(Winner < 0 ? "" : (await GetUserAsync(Winner))?.GetAvatarUrl(ImageFormat.Auto));
        }


        private static bool FindWinner(Board<Player> board, Player player, List<Pos> highlighted = null)
        {
            return board.FindLines(player, 3, highlighted);
        }


        private static bool IsTie(Board<Player> board, Player turn, int time)
        {
            if (time < board.Length - 3) return false;
            else if (time == board.Length) return true;

            turn = turn.Opponent;

            foreach (Pos pos in EmptyCells(board)) // Checks that all possible configurations result in a tie
            {
                var tempBoard = board.Copy();
                tempBoard[pos] = turn;
                if (FindWinner(tempBoard, turn) || !IsTie(tempBoard, turn, time + 1)) return false;
            }

            return true;
        }




        public override Task BotInputAsync()
        {
            // Win or block or random
            Pos target = TryCompleteLine(Turn) ?? TryCompleteLine(Turn.Opponent) ?? Program.Random.Choose(EmptyCells(board));
            return InputAsync($"{1 + target.y * board.Width + target.x}");
        }


        private Pos? TryCompleteLine(Player player)
        {
            uint count;
            Pos? missing;

            for (int y = 0; y < 3; y++) // Rows
            {
                count = 0;
                missing = null;
                for (int x = 0; x < 3; x++)
                {
                    if (board[x, y] == player) count++;
                    else if (board[x, y] == Player.None) missing = (x, y);
                    if (count == 2 && missing != null) return missing;
                }
            }

            for (int x = 0; x < 3; x++) // Columns
            {
                count = 0;
                missing = null;
                for (int y = 0; y < 3; y++)
                {
                    if (board[x, y] == player) count++;
                    else if (board[x, y] == Player.None) missing = (x, y);
                    if (count == 2 && missing != null) return missing;
                }
            }

            count = 0;
            missing = null;
            for (int d = 0; d < 3; d++) // Top-to-right diagonal
            {
                if (board[d, d] == player) count++;
                else if (board[d, d] == Player.None) missing = (d, d);
                if (count == 2 && missing != null) return missing;
            }

            count = 0;
            missing = null;
            for (int d = 0; d < 3; d++) // Top-to-left diagonal
            {
                if (board[2 - d, d] == player) count++;
                else if (board[2 - d, d] == Player.None) missing = (2 - d, d);
                if (count == 2 && missing != null) return missing;
            }

            return null;
        }


        private static List<Pos> EmptyCells(Board<Player> board)
        {
            return board.Positions.Where(p => board[p] == Player.None).ToList();
        }
    }
}
