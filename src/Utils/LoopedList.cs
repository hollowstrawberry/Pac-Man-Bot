using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace PacManBot.Utils
{
    /// <summary>
    /// Wrapper class for a <see cref="List{T}"/> whose index accessors loop around if out of bounds.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the list.</typeparam>
    [DataContract]
    public class LoopedList<T> : IList<T>
    {
        [DataMember] private readonly List<T> _list;


        /// <summary>Creates an instance that is a wrapper around a new <see cref="List{T}"/>.</summary>
        public LoopedList()
        {
            _list = new List<T>();
        }

        /// <summary>Creates a new <see cref="LoopedList{T}"/> that is a wrapper for the specified list.</summary>
        /// <exception cref="ArgumentNullException"/>
        public LoopedList(List<T> list)
        {
            this._list = list ?? throw new ArgumentNullException(nameof(list));
        }

        /// <summary>Creates a new <see cref="LoopedList{T}"/> that is a wrapper for a new list with the specified elements.</summary>
        /// <exception cref="ArgumentNullException"/>
        public LoopedList(IEnumerable<T> elements)
        {
            _list = new List<T>(elements ?? throw new ArgumentNullException(nameof(_list)));
        }




        /// <summary>Creates a shallow copy of the list.</summary>
        public LoopedList<T> Copy()
        {
            return new List<T>(_list);
        }


        /// <summary>Returns an index adjusted to loop around in case it's out of bounds from this list. 
        /// Indices are looped around automatically when accessing an element in the list.</summary>
        public int Wrapped(int index)
        {
            if (Count == 0) throw new InvalidOperationException("List contains no elements.");

            index %= Count;
            if (index < 0) index += Count;
            return index;
        }

        
        public static implicit operator LoopedList<T>(List<T> list) => new LoopedList<T>(list);

        public static implicit operator List<T>(LoopedList<T> list) => list._list;


        public static bool operator ==(LoopedList<T> left, LoopedList<T> right) => left.Equals(right);

        public static bool operator !=(LoopedList<T> left, LoopedList<T> right) => !(left == right);

        public override bool Equals(object obj) => obj is LoopedList<T> list && this._list == list._list;

        public override int GetHashCode() => _list.GetHashCode();




        // List interface

        public T this[int index]
        {
            get => _list[Wrapped(index)];
            set => _list[Wrapped(index)] = value;
        }

        public int Count => _list.Count;
        public bool IsReadOnly => false;

        public void Insert(int index, T item) => _list.Insert(Wrapped(index), item);
        public void RemoveAt(int index) => _list.RemoveAt(Wrapped(index));
        public void Add(T item) => _list.Add(item);
        public bool Remove(T item) => _list.Remove(item);
        public void Clear() => _list.Clear();
        public bool Contains(T item) => _list.Contains(item);
        public void CopyTo(T[] array, int arrayIndex) => _list.CopyTo(array, arrayIndex);
        public int IndexOf(T item) => _list.IndexOf(item);
        public IEnumerator<T> GetEnumerator() => _list.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => _list.GetEnumerator();
    }
}
