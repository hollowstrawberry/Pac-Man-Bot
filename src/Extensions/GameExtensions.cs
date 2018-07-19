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


            void CheckCell(Pos pos)
            {
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
                    CheckCell(new Pos(x, y));
                }
                line = new List<Pos>();
            }

            for (int x = 0; x < board.Width; x++) // Verticals
            {
                for (int y = 0; y < board.Height; y++)
                {
                    CheckCell(new Pos(x, y));
                }
                line = new List<Pos>();
            }

            for (int d = length - 1; d <= board.Width + board.Height - length; d++) // Top-to-left diagonals
            {
                for (int x, y = 0; y <= d; y++)
                {
                    if (y < board.Height && (x = d - y) < board.Width)
                    {
                        CheckCell(new Pos(x, y));
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
                        CheckCell(new Pos(x, y));
                    }
                }
                line = new List<Pos>();
            }

            return win;
        }
    }
}
