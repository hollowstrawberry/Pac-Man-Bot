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
    public class TTTGame : GameInstance
    {
        private static readonly TimeSpan _expiry = TimeSpan.FromMinutes(5);

        private Player[,] board;
        private List<Pos> highlighted;

        public override string Name => "Tic-Tac-Toe";
        public override TimeSpan Expiry => _expiry;

        public bool PlayingAI => client.GetUser(userId[(int)turn]).IsBot;



        public TTTGame(ulong channelId, ulong[] userId, DiscordShardedClient client, LoggingService logger, StorageService storage, int size = 3)
            : base(channelId, userId, client, logger, storage)
        {
            highlighted = new List<Pos>();
            board = new Player[size,size];
            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    board[x, y] = Player.None;
                }
            }

            if (PlayingAI) DoTurnAI();
        }



        public override bool IsInput(string value)
        {
            return int.TryParse(StripPrefix(value), out int num) && num > 0 && num <= board.Length;
        }


        public override void DoTurn(string rawInput)
        {
            base.DoTurn(rawInput);

            int input = int.Parse(StripPrefix(rawInput)) - 1;

            int y = input / board.LengthX();
            int x = input % board.LengthX();

            if (board[x, y] != Player.None) return; // Cell is already occupied

            board[x, y] = turn;
            
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

            for (int i = 0; i < userId.Length; i++)
            {
                description.Append($"{"►".If(i == (int)turn)}{((Player)i).Symbol()} - {User((Player)i).NameandNum().SanitizeMarkdown()}\n");
            }

            description.Append("ᅠ\n");

            for (int y = 0; y < board.LengthY(); y++)
            {
                for (int x = 0; x < board.LengthX(); x++)
                {
                    description.Append(board[x, y].Symbol(highlighted.Contains(new Pos(x, y))) ??
                        (state == State.Active ? $"{CustomEmoji.NumberCircle[1 + board.LengthX()*y + x]}" : Player.None.Circle()));
                }
                description.Append('\n');
            }

            if (state == State.Active) description.Append($"ᅠ\n*Say the number of a cell (1 to 9) to place an {(turn == Player.Red ? "X" : "O")}*");

            return new EmbedBuilder()
            {
                Title = EmbedTitle(),
                Description = description.ToString(),
                Color = turn.Color(),
                ThumbnailUrl = winner == Player.None ? turn.Symbol().ToEmote()?.Url : User(winner)?.GetAvatarUrl(),
            };
        }


        private static bool FindWinner(Player[,] board, Player player, List<Pos> highlighted = null)
        {
            return board.FindLines(player, 3, highlighted);
        }


        private static bool IsTie(Player[,] board, Player turn, int time)
        {
            if (time < board.LengthX() * board.LengthY() - 3) return false;
            else if (time == board.LengthX()*board.LengthY()) return true;

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
            Pos target = TryCompleteLine(turn) ?? TryCompleteLine(turn.OtherPlayer()); //Win or block
            if (target == null) target = GlobalRandom.Choose(EmptyCells(board));

            DoTurn($"{1 + target.y * board.LengthX() + target.x}");
        }


        private Pos TryCompleteLine(Player player)
        {
            uint count = 0;
            Pos missing = null;

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
