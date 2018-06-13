using System.Collections.Generic;
using Discord;
using PacManBot.Games;
using PacManBot.Utils;

namespace PacManBot.Extensions
{
    public static class GameExtensions
    {
        public static readonly Color[] PlayerColor = new Color[]
        {
            new Color(221, 46, 68), new Color(85, 172, 238), new Color(120, 177, 89), new Color(253, 203, 88), new Color(170, 142, 214),
        };


        // Game enums

        public static Color Color(this Player player)
        {
            if (player >= 0 && player <= EnumTraits<Player>.MaxValue) return PlayerColor[(int)player];
            else if (player == Player.Tie) return PlayerColor[(int)Player.Third];
            else return new Color(150, 150, 150);
        }


        public static string Circle(this Player player, bool highlighted = false)
        {
            switch (player)
            {
                case Player.First:  return highlighted ? CustomEmoji.C4redHL : CustomEmoji.C4red;
                case Player.Second: return highlighted ? CustomEmoji.C4blueHL : CustomEmoji.C4blue;
                case Player.None: return CustomEmoji.BlackCircle;
                default: return CustomEmoji.Staff;
            }
        }


        public static string Symbol(this Player player, bool highlighted = false)
        {
            switch (player)
            {
                case Player.First:  return highlighted ? CustomEmoji.TTTxHL : CustomEmoji.TTTx;
                case Player.Second: return highlighted ? CustomEmoji.TTToHL : CustomEmoji.TTTo;
                case Player.None: return null;
                default: return CustomEmoji.Staff;
            }
        }


        public static Player OtherPlayer(this Player player)
        {
            return player == Player.First ? Player.Second : Player.First;
        }


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


        public static Dir Opposite(this Dir dir)
        {
            switch (dir)
            {
                case Dir.up:    return Dir.down;
                case Dir.down:  return Dir.up;
                case Dir.left:  return Dir.right;
                case Dir.right: return Dir.left;

                default: return Dir.none;
            }
        }


        public static Pos OfLength(this Dir dir, int num)
        {
            if (num < 0) num = 0;
            Pos pos = new Pos(0, 0);
            for (int i = 0; i < num; i++) pos += dir;
            return pos;
        }




        // 2d arrays

        public static int X<T>(this T[,] board) => board.GetLength(0);

        public static int Y<T>(this T[,] board) => board.GetLength(1);


        public static T At<T>(this T[,] board, Pos pos, bool wrap = true)
        {
            if (wrap) board.Wrap(ref pos);
            return board[pos.x, pos.y];
        }


        public static void SetAt<T>(this T[,] board, Pos pos, T value, bool wrap = true)
        {
            if (wrap) board.Wrap(ref pos);
            board[pos.x, pos.y] = value;
        }


        public static void Wrap<T>(this T[,] board, ref Pos pos) // Wraps position if out of bounds
        {
            while (pos.x < 0) pos.x += board.X();
            while (pos.x >= board.X()) pos.x -= board.X();
            while (pos.y < 0) pos.y += board.Y();
            while (pos.y >= board.Y()) pos.y -= board.Y();
        }


        // Used in Tic-Tac-Toe and Connect 4
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

            for (int d = length - 1; d <= board.Y() + board.X() - length; d++) //Top-to-left diagonals
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

            for (int d = length - 1; d <= board.Y() + board.X() - length; d++) //Top-to-right diagonals
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
