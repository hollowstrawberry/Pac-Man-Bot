using System;
using System.Threading.Tasks;
using Discord.Commands;
using PacManBot.Games;

namespace PacManBot.Commands.Modules.GameModules
{
    [Name(ModuleNames.Games), Remarks("3")]
    public class MinesweeperModule : BaseModule
    {
        [Command("minesweeper"), Alias("mine")]
        [Remarks("Send a Minesweeper board in chat.")]
        [Summary("Sends a newly generated Minesweeper board in chat.\n" +
                 "You can specify a size between 5 and 12 (default 8), and a difficulty between 1 and 9 (default 3).\n\n" +
                 "The game is completely controlled by the user, with no interaction with the bot. " +
                 "To play, simply click a tile to reveal its contents. If it's a bomb, you lose. " +
                 "If it's a number, that number will indicate the number of bombs around that tile.\n" +
                 "You win once all decidedly non-bomb tiles have been uncovered.")]
        public async Task Minesweeper(int size = 8, int difficulty = 3)
        {
            if (size < 5 || size > 12)
            {
                await ReplyAsync("The board size must range between 5 and 12");
                return;
            }
            if (difficulty < 1 || difficulty > 9)
            {
                await ReplyAsync("The difficulty must range between 1 and 9");
                return;
            }

            var board = GenerateBoard(size, difficulty);
            await ReplyAsync(board.ToString(x => $"||{x}|| "));
        }



        private const string Bomb = "💥";

        private static readonly string[] Numbers =
        {
            ":zero:", ":one:", ":two:", ":three:", ":four:", ":five:", ":six:", ":seven:", ":eight:",
        };

        private static readonly Pos[] AdjacentPos =
        {
            (0, 1), (0, -1), (1, 0), (-1, 0), (1, 1), (1, -1), (-1, 1), (-1, -1),
        };


        public static Board<string> GenerateBoard(int size, int difficulty)
        {
            var board = new Board<string>(size, size, "");

            var totalBombs = size*size*(0.05 + 0.05*difficulty);
            int bombs = 0;
            while (bombs < totalBombs)
            {
                Pos p = (Program.Random.Next(size), Program.Random.Next(size));
                if (board[p] != Bomb)
                {
                    board[p] = Bomb;
                    bombs++;
                }
            }

            foreach (var pos in board.Positions)
            {
                if (board[pos] == Bomb) continue;

                bombs = 0;
                foreach (var p in AdjacentPos)
                {
                    if (board[pos + p] == Bomb) bombs++;
                }
                board[pos] = Numbers[bombs];
            }

            return board;
        }
    }
}
