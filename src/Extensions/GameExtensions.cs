using System.Collections.Generic;
using Discord;
using PacManBot.Games;
using PacManBot.Utils;
using PacManBot.Constants;

namespace PacManBot.Extensions
{
    public static class GameExtensions
    {
        /// <summary>Standard player colors for most multiplayer games</summary>
        public static readonly Color[] PlayerColor = {
            Colors.Red, Colors.Blue, Colors.Green, Colors.Yellow, Colors.Purple, Colors.Orange,
        };




        // Games


        /// <summary>The unique id that identifies this game, which will be the channel ID it is located in
        /// if it is a channel game, or the owner's user ID if it is a user-specific game.</summary>
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


        /// <summary>Returns a constant <see cref="Discord.Color"/> corresponding with a <see cref="Player"/>.</summary>
        public static Color Color(this Player player)
        {
            if (player >= 0 && player <= EnumTraits<Player>.MaxValue) return PlayerColor[(int)player];
            if (player == Player.Tie) return Colors.Green;
            return Colors.Gray;
        }


        /// <summary>Returns a constant circle custom emoji corresponding with a <see cref="Player"/>.</summary>
        public static string Circle(this Player player, bool highlighted = false)
        {
            switch (player)
            {
                case Player.First:  return highlighted ? CustomEmoji.C4redHL : CustomEmoji.C4red;
                case Player.Second: return highlighted ? CustomEmoji.C4blueHL : CustomEmoji.C4blue;
                case Player.None:   return CustomEmoji.BlackCircle;
                default: return CustomEmoji.Staff;
            }
        }


        /// <summary>Returns a constant Tic-Tac-Toe symbol custom emoji corresponding with a <see cref="Player"/>.</summary>
        public static string Symbol(this Player player, bool highlighted = false)
        {
            switch (player)
            {
                case Player.First:  return highlighted ? CustomEmoji.TTTxHL : CustomEmoji.TTTx;
                case Player.Second: return highlighted ? CustomEmoji.TTToHL : CustomEmoji.TTTo;
                case Player.None:   return null;
                default: return CustomEmoji.Staff;
            }
        }


        /// <summary>Returns the opposing player, directed at two-player games.</summary>
        public static Player OtherPlayer(this Player player)
        {
            return player == Player.First ? Player.Second : Player.First;
        }


        /// <summary>Returns a color-based name for a given <see cref="Player"/>.</summary>
        public static string ToStringColor(this Player player)
        {
            switch (player)
            {
                case Player.First:  return "Red";
                case Player.Second: return "Blue";
                case Player.Third:  return "Green";
                case Player.Fourth: return "Yellow";
                case Player.Fifth:  return "Purple";

                default: return "???";
            }
        }


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
                case Dir.Up:    return new Pos(0, -length);
                case Dir.Down:  return new Pos(0, +length);
                case Dir.Left:  return new Pos(-length, 0);
                case Dir.Right: return new Pos(+length, 0);
                default: return Pos.Origin;
            }
        }




        // 2d arrays

        /// <summary>Returns the horizontal length of a 2d array.</summary>
        public static int X<T>(this T[,] board) => board.GetLength(0);

        /// <summary>Returns the vertical length of a 2d array.</summary>
        public static int Y<T>(this T[,] board) => board.GetLength(1);


        /// <summary>
        /// Returns the value at the specified coordinates in a 2d array. 
        /// If <paramref name="wrap"/> is true, the coordinates will wrap around instead of
        /// throwing an error when they are out of bounds.
        /// </summary>
        public static T At<T>(this T[,] board, Pos pos, bool wrap = true)
        {
            if (wrap) board.Wrap(ref pos);
            return board[pos.x, pos.y];
        }


        /// <summary>
        /// Sets a value at the specified coordinates in a 2d array. 
        /// If <paramref name="wrap"/> is true, the coordinates will wrap around instead of
        /// throwing an error when they are out of bounds.
        /// </summary>
        public static void SetAt<T>(this T[,] board, Pos pos, T value, bool wrap = true)
        {
            if (wrap) board.Wrap(ref pos);
            board[pos.x, pos.y] = value;
        }


        /// <summary>If the given coordinates are out of bounds for the given 2d array, 
        /// they will be adjusted to wrap around, coming from the other side, until they are in bounds.</summary>
        public static void Wrap<T>(this T[,] board, ref Pos pos)
        {
            pos.x %= board.X();
            if (pos.x < 0) pos.x += board.X();

            pos.y %= board.Y();
            if (pos.y < 0) pos.y += board.Y();
        }



        /// <summary>
        /// Finds all horizontal, vertical, and diagonal lines consisted of values equal to the provided value,
        /// inside a 2d array. The positions of the lines will be stored inside <paramref name="result"/>, and the return value
        /// indicates if any lines were found. Lines with length exceeding the one provided won't be counted more than once.
        /// To be used in games such as Tic-Tac-Toe and Connect 4.
        /// </summary>
        /// <param name="board">The board to find lines in.</param>
        /// <param name="value">The value to find lines of.</param>
        /// <param name="length">The expected line length. Lines exceeding this length won't be counted more than once.</param>
        /// <param name="result">The list where the positions of found lines will be stored, if provided.</param>
        /// <returns>Whether any lines were found.</returns>
        public static bool FindLines<T>(this T[,] board, T value, int length, List<Pos> result = null)
        {
            bool win = false;
            List<Pos> line = new List<Pos>();


            void CheckCell(Pos pos)
            {
                if (board.At(pos).Equals(value))
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


            for (int y = 0; y < board.Y(); y++) // Horizontals
            {
                for (int x = 0; x < board.X(); x++)
                {
                    CheckCell(new Pos(x, y));
                }
                line = new List<Pos>();
            }

            for (int x = 0; x < board.X(); x++) // Verticals
            {
                for (int y = 0; y < board.Y(); y++)
                {
                    CheckCell(new Pos(x, y));
                }
                line = new List<Pos>();
            }

            for (int d = length - 1; d <= board.Y() + board.X() - length; d++) // Top-to-left diagonals
            {
                for (int x, y = 0; y <= d; y++)
                {
                    if (y < board.Y() && (x = d - y) < board.X())
                    {
                        CheckCell(new Pos(x, y));
                    }
                }
                line = new List<Pos>();
            }

            for (int d = length - 1; d <= board.Y() + board.X() - length; d++) // Top-to-right diagonals
            {
                for (int x, y = 0; y <= d; y++)
                {
                    if (y < board.Y() && (x = board.X() - 1 - d + y) >= 0)
                    {
                        CheckCell(new Pos(x, y));
                    }
                }
                line = new List<Pos>();
            }

            return win;
        }
    }
}
