using System;
using PacManBot.Extensions;

namespace PacManBot.Games
{
    /// <summary>
    /// A position in a <see cref="Board{T}"/>, expressed as x and y coordinates.
    /// </summary>
    public struct Pos : IEquatable<Pos>
    {
        public static readonly Pos Origin = (0, 0);


        public readonly int x;
        public readonly int y;

        public Pos(int x, int y)
        {
            this.x = x;
            this.y = y;
        }


        public static implicit operator Pos((int x, int y) tuple) => new Pos(tuple.x, tuple.y);


        public override string ToString() => $"({x},{y})";

        public override int GetHashCode() => x.GetHashCode() ^ y.GetHashCode();

        public override bool Equals(object obj) => obj is Pos pos && this == pos;

        public bool Equals(Pos pos) => this == pos;


        public static bool operator ==(Pos pos1, Pos pos2) => pos1.x == pos2.x && pos1.y == pos2.y;

        public static bool operator !=(Pos pos1, Pos pos2) => !(pos1 == pos2);

        public static Pos operator +(Pos pos1, Pos pos2) => new Pos(pos1.x + pos2.x, pos1.y + pos2.y);

        public static Pos operator -(Pos pos1, Pos pos2) => new Pos(pos1.x - pos2.x, pos1.y - pos2.y);

        public static Pos operator +(Pos pos, Dir dir) => pos + dir.ToPos();

        public static Pos operator *(Pos pos, int mult) => new Pos(pos.x * mult, pos.y * mult);


        public static float Distance(Pos pos1, Pos pos2)
        {
            return (float)Math.Sqrt(Math.Pow(pos2.x - pos1.x, 2) + Math.Pow(pos2.y - pos1.y, 2));
        }
    }
}
