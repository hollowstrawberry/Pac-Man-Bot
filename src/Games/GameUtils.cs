using System;
using Discord;
using PacManBot.Constants;

namespace PacManBot.Games
{
    public static class GameUtils
    {
        public static readonly Random GlobalRandom = new Random();


        // Data types

        public class Pos // 2d coordinates
        {
            public int x, y;

            public Pos(int x, int y)
            {
                this.x = x;
                this.y = y;
            }

            public override int GetHashCode() => base.GetHashCode();

            public override bool Equals(object obj)
            {
                if (this is null || obj is null) return this is null && obj is null;
                return obj is Pos pos && x == pos.x && y == pos.y;
            }

            public static bool operator ==(Pos pos1, Pos pos2) => pos1.Equals(pos2);
            public static bool operator !=(Pos pos1, Pos pos2) => !pos1.Equals(pos2);
            public static Pos operator +(Pos pos1, Pos pos2) => new Pos(pos1.x + pos2.x, pos1.y + pos2.y);
            public static Pos operator -(Pos pos1, Pos pos2) => new Pos(pos1.x - pos2.x, pos1.y - pos2.y);

            public static Pos operator +(Pos pos, Dir dir) //Moves position in the given direction
            {
                switch (dir)
                {
                    case Dir.up: return new Pos(pos.x, pos.y - 1);
                    case Dir.down: return new Pos(pos.x, pos.y + 1);
                    case Dir.left: return new Pos(pos.x - 1, pos.y);
                    case Dir.right: return new Pos(pos.x + 1, pos.y);
                    default: return pos;
                }
            }

            public static float Distance(Pos pos1, Pos pos2) => (float)Math.Sqrt(Math.Pow(pos2.x - pos1.x, 2) + Math.Pow(pos2.y - pos1.y, 2));
        }


        public enum State
        {
            Active, Ended, Lose, Win,
        }


        public enum Player
        {
            Red, Blue, Tie, None,
        }


        public enum GameInput
        {
            Zero, One, Two, Three, Four, Five, Six, Seven, Eight, Nine,
            Up, Left, Down, Right, Wait, Help, Fast,
            None = -1,
        }


        public enum Dir
        {
            none, up, left, down, right,
        }




        // 2d array extension methods

        public static int LengthX<T>(this T[,] board) => board.GetLength(0);

        public static int LengthY<T>(this T[,] board) => board.GetLength(1);

        public static T At<T>(this T[,] board, Pos pos)
        {
            board.Wrap(ref pos);
            return board[pos.x, pos.y];
        }

        public static void SetAt<T>(this T[,] board, Pos pos, T value)
        {
            board.Wrap(ref pos);
            board[pos.x, pos.y] = value;
        }

        public static void Wrap<T>(this T[,] board, ref Pos pos) //Wraps the position from one side of the board to the other if it's out of bounds
        {
            if (pos.x < 0) pos.x += board.LengthX();
            else if (pos.x >= board.LengthX()) pos.x -= board.LengthX();
            if (pos.y < 0) pos.y += board.LengthY();
            else if (pos.y >= board.LengthY()) pos.y -= board.LengthY();
        }



        // Enum extension methods

        public static Color Color(this Player player)
        {
            switch (player)
            {
                case Player.Red: return new Color(221, 46, 68);
                case Player.Blue: return new Color(46, 126, 221);
                case Player.Tie: return new Color(120, 177, 89);
                default: return new Color(150, 150, 150);
            }
        }

        public static string Circle(this Player player)
        {
            switch (player)
            {
                case Player.Red: return "🔴";
                case Player.Blue: return "🔵";
                case Player.None: return "⚫";
                default: return $"{CustomEmoji.Staff}";
            }
        }


        public static Dir Opposite(this Dir dir)
        {
            switch (dir)
            {
                case Dir.up: return Dir.down;
                case Dir.down: return Dir.up;
                case Dir.left: return Dir.right;
                case Dir.right: return Dir.left;
                default: return Dir.none;
            }
        }


        public static Pos OfLength(this Dir dir, int num) //Converts a direction into what's essentially a vector
        {
            if (num < 0) num = 0;
            Pos pos = new Pos(0, 0);
            for (int i = 0; i < num; i++) pos += dir;
            return pos;
        }
    }
}
