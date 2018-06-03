using System;
using System.Text;
using System.Collections.Generic;
using Discord;
using Discord.WebSocket;
using PacManBot.Constants;
using PacManBot.Services;
using static PacManBot.Games.GameUtils;

namespace PacManBot.Games
{
    public class TTTGame : MultiplayerGame, IMessagesGame
    {
        private const int Size = 3;
        private static readonly TimeSpan _expiry = TimeSpan.FromHours(1);

        private Player[,] board;
        private List<Pos> highlighted;

        public override string Name => "Tic-Tac-Toe";
        public override TimeSpan Expiry => _expiry;


        public override void Create(ulong channelId, ulong[] userId, DiscordShardedClient client, LoggingService logger, StorageService storage)
        {
            base.Create(channelId, userId, client, logger, storage);

            highlighted = new List<Pos>();
            board = new Player[Size,Size];
            for (int x = 0; x < Size; x++)
            {
                for (int y = 0; y < Size; y++)
                {
                    board[x, y] = Player.None;
                }
            }
        }



        public bool IsInput(string value, ulong userId)
        {
            return userId == User(Turn).Id && int.TryParse(StripPrefix(value), out int num) && num > 0 && num <= board.Length;
        }


        public void DoTurn(string input)
        {
            int cell = int.Parse(StripPrefix(input)) - 1;
            int y = cell / board.X();
            int x = cell % board.X();

            if (State != State.Active || board[x, y] != Player.None) return;

            board[x, y] = Turn;
            Time++;
            LastPlayed = DateTime.Now;

            if (FindWinner(board, Turn, highlighted)) Winner = Turn;
            else if (IsTie(board, Turn, Time)) Winner = Player.Tie;

            if (Winner == Player.None)
            {
                Turn = Turn.OtherPlayer();
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
                description.Append($"{"►".If(i == (int)Turn)}{((Player)i).Symbol()} - {User((Player)i).NameandNum().SanitizeMarkdown()}\n");
            }

            description.Append("ᅠ\n");

            for (int y = 0; y < board.Y(); y++)
            {
                for (int x = 0; x < board.X(); x++)
                {
                    description.Append(board[x, y].Symbol(highlighted.Contains(new Pos(x, y))) ??
                        (State == State.Active ? $"{CustomEmoji.NumberCircle[1 + board.X()*y + x]}" : Player.None.Circle()));
                }
                description.Append('\n');
            }

            if (State == State.Active) description.Append($"ᅠ\n*Say the number of a cell (1 to 9) to place an {(Turn == Player.First ? "X" : "O")}*");

            return new EmbedBuilder()
            {
                Title = EmbedTitle(),
                Description = description.ToString(),
                Color = Turn.Color(),
                ThumbnailUrl = Winner == Player.None ? Turn.Symbol().ToEmote()?.Url : User(Winner)?.GetAvatarUrl(),
            };
        }


        private static bool FindWinner(Player[,] board, Player player, List<Pos> highlighted = null)
        {
            return board.FindLines(player, 3, highlighted);
        }


        private static bool IsTie(Player[,] board, Player turn, int time)
        {
            if (time < board.X() * board.Y() - 3) return false;
            else if (time == board.X()*board.Y()) return true;

            turn = turn.OtherPlayer();

            foreach (Pos pos in EmptyCells(board)) // Checks that all possible configurations result in a tie
            {
                var tempBoard = (Player[,])board.Clone();
                tempBoard.SetAt(pos, turn);
                if (FindWinner(tempBoard, turn) || !IsTie(tempBoard, turn, time + 1)) return false;
            }

            return true;
        }




        public override void DoTurnAI()
        {
            Pos target = TryCompleteLine(Turn) ?? TryCompleteLine(Turn.OtherPlayer()) ?? Bot.Random.Choose(EmptyCells(board)); //Win or block or random
            DoTurn($"{1 + target.y * board.X() + target.x}");
        }


        private Pos? TryCompleteLine(Player player)
        {
            uint count = 0;
            Pos? missing = null;

            for (int y = 0; y < 3; y++) // Rows
            {
                for (int x = 0; x < 3; x++)
                {
                    if (board[x, y] == player) count++;
                    else if (board[x, y] == Player.None) missing = new Pos(x, y);
                    if (count == 2 && missing != null) return missing;
                }
                count = 0;
                missing = null;
            }

            for (int x = 0; x < 3; x++) // Columns
            {
                for (int y = 0; y < 3; y++)
                {
                    if (board[x, y] == player) count++;
                    else if (board[x, y] == Player.None) missing = new Pos(x, y);
                    if (count == 2 && missing != null) return missing;
                }
                count = 0;
                missing = null;
            }

            for (int d = 0; d < 3; d++) // Top-to-right diagonal
            {
                if (board[d, d] == player) count++;
                else if (board[d, d] == Player.None) missing = new Pos(d, d);
                if (count == 2 && missing != null) return missing;
            }
            count = 0;
            missing = null;

            for (int d = 0; d < 3; d++) // Top-to-left diagonal
            {
                if (board[2 - d, d] == player) count++;
                else if (board[2 - d, d] == Player.None) missing = new Pos(2 - d, d);
                if (count == 2 && missing != null) return missing;
            }

            return null;
        }


        private static List<Pos> EmptyCells(Player[,] board)
        {
            List<Pos> empty = new List<Pos>();
            for (int y = 0; y < board.Y(); y++)
            {
                for (int x = 0; x < board.X(); x++)
                {
                    if (board[x, y] == Player.None) empty.Add(new Pos(x, y));
                }
            }
            return empty;
        }
    }
}
