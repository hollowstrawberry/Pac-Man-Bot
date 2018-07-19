using System;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;
using System.Collections;

namespace PacManBot.Games
{
    /// <summary>
    /// Wrapper for a 2d array. Allows looping and accessing values using <see cref="Pos"/> coordinates.
    /// </summary>
    /// <typeparam name="T">The type of the values stored in the board.</typeparam>
    [DataContract]
    public class Board<T> : IEnumerable<T>, IEnumerable, IEquatable<Board<T>>, ICloneable
    {
        [DataMember] protected readonly T[,] values;
        [DataMember] protected readonly bool loop;


        /// <summary>Number of elements in the board.</summary>
        public int Length => values.Length;

        /// <summary>Number of columns in the board.</summary>
        public int Width => values.GetLength(0);

        /// <summary>Number of rows in the board.</summary>
        public int Height => values.GetLength(1);



        /// <summary>Creates a new board of the specified width and height.</summary>
        /// <param name="width">The constant width of the board.</param>
        /// <param name="height">The constant height of the board.</param>
        /// <param name="loop">Whether to loop around an out-of-range index instead of throwing an exception.</param>
        public Board(int width, int height, bool loop = true)
        {
            if (width < 1 || height < 1) throw new ArgumentException("The board must have a with and height of at least 1.");

            values = new T[width, height];
            this.loop = loop;
        }

        /// <summary>Creates a new board that is a wrapper for the specified 2d array.</summary>
        /// <param name="values">The array of values to use for the board.</param>
        /// <param name="loop">Whether to loop around an out-of-range index instead of throwing an exception.</param>
        public Board(T[,] values, bool loop = true)
        {
            this.values = values ?? throw new ArgumentNullException(nameof(values));
            this.loop = loop;
        }


        /// <summary>Retrieves an element in the board at the given position</summary>
        public T this[int x, int y]
        {
            get => this[new Pos(x, y)];
            set => this[new Pos(x, y)] = value;
        }

        /// <summary>Retrieves an element in the board at the given position</summary>
        public T this[Pos pos]
        {
            get
            {
                if (loop) Wrap(ref pos);

                try
                {
                    return values[pos.x, pos.y];
                }
                catch (IndexOutOfRangeException e)
                {
                    throw new IndexOutOfRangeException(
                        $"The position {pos} is outside the board (width {Width}, height {Height}) " +
                        $"and the board doesn't loop around.", e);
                }
            }

            set
            {
                if (loop) Wrap(ref pos);

                try
                {
                    values[pos.x, pos.y] = value;
                }
                catch (IndexOutOfRangeException e)
                {
                    throw new IndexOutOfRangeException(
                        $"The position {pos} is outside the board (width {Width}, height {Height}) " +
                        $"and the board doesn't loop around.", e);
                }
            }
        }


        public static implicit operator Board<T>(T[,] array) => new Board<T>(array);

        public static explicit operator T[,] (Board<T> board) => board.values;

        public static bool operator ==(Board<T> left, Board<T> right) => left.Equals(right);

        public static bool operator !=(Board<T> left, Board<T> right) => !(left == right);


        /// <summary>Returns a new board that is a wrapper for a copy of the original 2d array.</summary>
        public Board<T> Copy()
        {
            return new Board<T>((T[,])values.Clone(), loop);
        }

        /// <summary>Replaces all the elements in the board with the specified value.</summary>
        public void Fill(T value)
        {
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    values[x, y] = value;
                }
            }
        }


        /// <summary>Replaces all the elements in the board using the specified value selector.</summary>
        /// <param name="valueSelector">A delegate that returns the desired value at the given position.</param>
        public void Fill(Func<Pos, T> valueSelector)
        {
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    values[x, y] = valueSelector(new Pos(x, y));
                }
            }
        }


        /// <summary>If the given position is outside the board, it will be adjusted to loop around, 
        /// coming out from the other side. This is done automatically when accessing a board that loops.</summary>
        public void Wrap(ref Pos pos)
        {
            pos.x %= Width;
            if (pos.x < 0) pos.x += Width;

            pos.y %= Height;
            if (pos.y < 0) pos.y += Height;
        }


        /// <summary>Builds and returns a string representing all the elements in the board.</summary>
        public override string ToString()
        {
            var sb = new StringBuilder();
            for (int y = 0; y < Height; y++)
            {
                if (y > 0) sb.Append('\n');
                for (int x = 0; x < Width; x++)
                {
                    sb.Append(values[x, y]);
                }
            }

            return sb.ToString();
        }


        /// <summary>Builds and returns a string representing the board, 
        /// composed of values obtained using the specified selector.</summary>
        /// <param name="stringSelector">A delegate that takes a position's value and returns its desired string representation.</param>
        public string ToString(Func<T, string> stringSelector)
        {
            var sb = new StringBuilder();
            for (int y = 0; y < Height; y++)
            {
                if (y > 0) sb.Append('\n');
                for (int x = 0; x < Width; x++)
                {
                    sb.Append(stringSelector(values[x, y]));
                }
            }

            return sb.ToString();
        }


        public override bool Equals(object obj) => obj is Board<T> board && this.Equals(board);

        public override int GetHashCode() => values.GetHashCode();


        // Interfaces

        public bool Equals(Board<T> other) => values == other.values;

        public object Clone() => Copy();

        IEnumerator IEnumerable.GetEnumerator() => values.GetEnumerator();

        IEnumerator<T> IEnumerable<T>.GetEnumerator() => values.Cast<T>().GetEnumerator();
    }
}
