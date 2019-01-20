using System.Collections.Generic;
using PacManBot.Constants;
using PacManBot.Games;

namespace PacManBot.Extensions
{
    public static class GameExtensions
    {
        // Games

        /// <summary>The unique id that identifies a game,
        /// which can be its owner's user ID or the housing channel's ID depending on the type.</summary>
        public static ulong IdentifierId(this IBaseGame game)
        {
            switch (game)
            {
                case IUserGame userGame: return userGame.OwnerId;
                case IChannelGame channelGame: return channelGame.ChannelId;
                default: return game.UserId[0];
            }
        }


        /// <summary>A stored game's unique filename based on its type and identifying ID.</summary>
        public static string GameFile(this IStoreableGame game)
        {
            return $"{Files.GameFolder}{game.FilenameKey}{game.IdentifierId()}{Files.GameExtension}";
        }




        // Game enums

        /// <summary>Returns the opposite direction to the one provided.</summary>
        public static Dir Opposite(this Dir dir)
        {
            switch (dir)
            {
                case Dir.Up:    return Dir.Down;
                case Dir.Down:  return Dir.Up;
                case Dir.Left:  return Dir.Right;
                case Dir.Right: return Dir.Left;

                default: return Dir.None;
            }
        }


        /// <summary>Converts a direction into a <see cref="Pos"/> vector with the given length.</summary>
        public static Pos ToPos(this Dir dir, int length = 1)
        {
            switch (dir)
            {
                case Dir.Up:    return (0, -length);
                case Dir.Down:  return (0, +length);
                case Dir.Left:  return (-length, 0);
                case Dir.Right: return (+length, 0);
                default: return Pos.Origin;
            }
        }




        // Board

        /// <summary>
        /// Finds all horizontal, vertical, and diagonal lines consisted of values equal to the specified value, 
        /// inside a board.
        /// </summary>
        /// <remarks>To be used in games such as Tic-Tac-Toe and Connect 4.</remarks>
        /// <param name="board">The board to find lines in.</param>
        /// <param name="value">The value to find lines of.</param>
        /// <param name="length">The required minimum length. Lines exceeding this length will only be counted once.</param>
        /// <param name="result">The list where the positions of found lines will be stored, if provided.</param>
        /// <returns>Whether any lines were found.</returns>
        public static bool FindLines<T>(this Board<T> board, T value, int length, List<Pos> result = null)
        {
            bool win = false;
            List<Pos> line = new List<Pos>();


            void CheckCell(int x, int y)
            {
                Pos pos = (x, y);
                if (board[pos].Equals(value))
                {
                    line.Add(pos);

                    if (line.Count >= length)
                    {
                        win = true;
                        if (result != null)
                        {
                            if (line.Count == length)
                            {
                                foreach (Pos p in line) result.Add(p);
                            }
                            else result.Add(pos); // Above minimum length
                        }
                    }
                }
                else line = new List<Pos>();
            }


            for (int y = 0; y < board.Height; y++) // Horizontals
            {
                for (int x = 0; x < board.Width; x++)
                {
                    CheckCell(x, y);
                }
                line = new List<Pos>();
            }

            for (int x = 0; x < board.Width; x++) // Verticals
            {
                for (int y = 0; y < board.Height; y++)
                {
                    CheckCell(x, y);
                }
                line = new List<Pos>();
            }

            for (int d = length - 1; d <= board.Width + board.Height - length; d++) // Top-to-left diagonals
            {
                for (int x, y = 0; y <= d; y++)
                {
                    if (y < board.Height && (x = d - y) < board.Width)
                    {
                        CheckCell(x, y);
                    }
                }
                line = new List<Pos>();
            }

            for (int d = length - 1; d <= board.Width + board.Height - length; d++) // Top-to-right diagonals
            {
                for (int x, y = 0; y <= d; y++)
                {
                    if (y < board.Height && (x = board.Width - 1 - d + y) >= 0)
                    {
                        CheckCell(x, y);
                    }
                }
                line = new List<Pos>();
            }

            return win;
        }
    }
}
