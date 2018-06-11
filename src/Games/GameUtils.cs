using System;

namespace PacManBot.Games
{
    public enum State
    {
        Active,
        Completed,
        Cancelled,
        Lose,
        Win,
    }


    public enum Player
    {
        First, Second, Third, Fourth, Fifth, Sixth, Seventh, Eighth, Nineth, Tenth,
        None = -1,
        Tie = -2,
    }


    public enum Dir
    {
        none,
        up,
        left,
        down,
        right,
    }


    public struct Pos
    {
        public int x;
        public int y;

        public Pos(int x, int y)
        {
            this.x = x;
            this.y = y;
        }

        public override string ToString() => $"({x},{y})";
        public override int GetHashCode() => x.GetHashCode() ^ y.GetHashCode();
        public override bool Equals(object obj) => obj is Pos pos && this == pos;

        public static bool operator ==(Pos pos1, Pos pos2) => pos1.x == pos2.x && pos1.y == pos2.y;
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
}
