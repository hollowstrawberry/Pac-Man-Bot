using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using PacManBot.Extensions;
using PacManBot.Games;
using PacManBot.Utils;

namespace PacManBot.Commands.Modules
{
    [Module(ModuleNames.Games)]
    public class MinesweeperModule : BasePmBotModule
    {
        private const string BombChar = "💥";

        private static readonly string[] NumberChars = new Range(9)
            .Select(x => (char)('\U00000030' + x) + "\U000020e3").ToArray(); // Two-character emoji


        private const int Bomb = -1;

        private static readonly Pos[] AdjacentPos = {
            (0, 1), (0, -1), (1, 0), (-1, 0), (1, 1), (1, -1), (-1, 1), (-1, -1),
        };


        [Command("minesweeper"), Aliases("ms")]
        [Description(
            "Sends a newly generated Minesweeper board in chat.\n" +
            "You can specify a size between 5 and 14 (default 8), and a difficulty between 1 and 9 (default 3).\n\n" +
            "The game is completely controlled by the user, with no interaction with the bot. " +
            "To play, simply click a tile to reveal its contents. If it's a bomb, you lose. " +
            "If it's a number, that number will indicate the number of bombs around that tile.\n" +
            "The top-left tile is never a bomb. You win once all non-bomb tiles have been uncovered!")]
        public async Task Minesweeper(CommandContext ctx, int size = 8, int difficulty = 3)
        {
            if (size < 5 || size > 14)
            {
                await ctx.RespondAsync("The board size must range between 5 and 14");
                return;
            }
            if (difficulty < 1 || difficulty > 9)
            {
                await ctx.RespondAsync("The difficulty must range between 1 and 9");
                return;
            }

            var board = GenerateBoard(size, difficulty);
            var content = board.ToString(x => $"||{(x == Bomb ? BombChar : NumberChars[x])}|| ");
            await ctx.RespondAsync(content);
        }


        private static Board<int> GenerateBoard(int size, int difficulty)
        {
            var board = new Board<int>(size, size);

            int totalBombs = (size*size*(0.05 + 0.05*difficulty)).Floor();
            var bombs = new List<Pos>(totalBombs);
            while (bombs.Count < totalBombs) // Add bombs
            {
                Pos p = (Program.Random.Next(size), Program.Random.Next(size));
                if (p != (0, 0) && board[p] != Bomb)
                {
                    board[p] = Bomb;
                    bombs.Add(p);
                }
            }

            foreach (var bomb in bombs) // Add numbers around bombs
            {
                foreach (var adj in AdjacentPos.Select(p => bomb + p))
                {
                    if (adj.x < 0 || adj.x >= board.Width || adj.y < 0 || adj.y >= board.Height) continue;
                    if (board[adj] == Bomb) continue;
                    board[adj]++;
                }
            }

            return board;
        }
    }
}
