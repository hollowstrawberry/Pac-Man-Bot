using System;
using System.Collections.Generic;
using System.Diagnostics;
using Discord;
using PacManBot.Constants;

namespace PacManBot.Games
{
    public static class GameUtils
    {
        public static readonly Random GlobalRandom = new Random();


        public static readonly string[] StartTexts = new string[]
        {
            "I'll give it a go", "Let's do this", "Dare to defy the gamemaster?", "May the best win", "I was getting bored!", "Maybe you should play with a real person instead",
            "In need of friends to play with?"
        };
        public static readonly string[] GameTexts = new string[]
        {
            "🤔", "🔣", "🤖", $"{CustomEmoji.Thinkxel}", $"{CustomEmoji.PacMan}", "Hmm...", "Nice move.", "Take this!", "Huh.", "Aha!", "Come on now", "All according to plan",
            "I think I'm winning this one", "Beep boop", "Boop?", "Interesting...", "Recalculating...", "ERROR: YourSkills not found", "I wish to be a real bot", "That's all you got?",
            "Let's see what happens", "I don't even know what I'm doing", "This is a good time for you to quit", "Curious."
        };
        public static readonly string[] WinTexts = new string[]
        {
            "👍", $"{CustomEmoji.PacMan}", $"{CustomEmoji.Dance}", "Rekt", "Better luck next time", "Beep!", ":)", "Nice", "Muahaha", "You weren't even trying"
        };
        public static readonly string[] NotWinTexts = new string[]
        {
            "Oof", "No u", "Foiled again!", "Boo...", "Ack", "Good job!", "gg", "You're good at this", "I let you win, of course"
        };




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
            public override bool Equals(object obj) => obj is Pos pos && this == pos;

            public static bool operator ==(Pos pos1, Pos pos2)
            {
                if (pos1 is null || pos2 is null) return pos1 is null && pos2 is null;
                return pos1.x == pos2.x && pos1.y == pos2.y;
            }
            public static bool operator !=(Pos pos1, Pos pos2) => !(pos1 == pos2);
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
            Active, Completed, Cancelled, Lose, Win,
        }


        public enum Player
        {
            Red, Blue, Tie, None,
        }


        public enum Dir
        {
            none, up, left, down, right,
        }




        // 2d array extension methods

        public static int LengthX<T>(this T[,] board) => board.GetLength(0);

        public static int LengthY<T>(this T[,] board) => board.GetLength(1);

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

        public static void Wrap<T>(this T[,] board, ref Pos pos) //Wraps the position from one side of the board to the other if it's out of bounds
        {
            while (pos.x < 0) pos.x += board.LengthX();
            while (pos.x >= board.LengthX()) pos.x -= board.LengthX();
            while (pos.y < 0) pos.y += board.LengthY();
            while (pos.y >= board.LengthY()) pos.y -= board.LengthY();
        }



        // Enum extension methods

        public static Color Color(this Player player)
        {
            switch (player)
            {
                case Player.Red: return new Color(221, 46, 68);
                case Player.Blue: return new Color(85, 172, 238);
                case Player.Tie: return new Color(120, 177, 89);
                default: return new Color(150, 150, 150);
            }
        }

        public static string Circle(this Player player, bool highlighted = false)
        {
            switch (player)
            {
                case Player.Red: return (highlighted ? CustomEmoji.C4redHL : CustomEmoji.C4red).ToString();
                case Player.Blue: return (highlighted ? CustomEmoji.C4blueHL : CustomEmoji.C4blue).ToString();
                case Player.None: return "⚫";
                default: return CustomEmoji.Staff.ToString();
            }
        }

        public static string Symbol(this Player player, bool highlighted = false)
        {
            switch (player)
            {
                case Player.Red: return (highlighted ? CustomEmoji.TTTxHL : CustomEmoji.TTTx).ToString();
                case Player.Blue: return (highlighted ? CustomEmoji.TTToHL : CustomEmoji.TTTo).ToString();
                case Player.None: return null;
                default: return CustomEmoji.Staff.ToString();
            }
        }

        public static Player OtherPlayer(this Player player)
        {
            return player == Player.Red ? Player.Blue : Player.Red;
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




        // For Tic-Tac-Toe (expandable) and Connect Four

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


            for (int y = 0; y < board.LengthY(); y++) // Horizontals
            {
                for (int x = 0; x < board.LengthX(); x++)
                {
                    CheckCell(new Pos(x, y));
                }
                line = new List<Pos>();
            }

            for (int x = 0; x < board.LengthX(); x++) // Verticals
            {
                for (int y = 0; y < board.LengthY(); y++)
                {
                    CheckCell(new Pos(x, y));
                }
                line = new List<Pos>();
            }

            for (int d = length - 1; d <= board.LengthY() + board.LengthX() - length; d++) //Top-to-left diagonals
            {
                for (int x, y = 0; y <= d; y++)
                {
                    if (y < board.LengthY() && (x = d - y) < board.LengthX())
                    {
                        CheckCell(new Pos(x, y));
                    }
                }
                line = new List<Pos>();
            }

            for (int d = length - 1; d <= board.LengthY() + board.LengthX() - length; d++) //Top-to-right diagonals
            {
                for (int x, y = 0; y <= d; y++)
                {
                    if (y < board.LengthY() && (x = board.LengthX() - 1 - d + y) >= 0)
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
