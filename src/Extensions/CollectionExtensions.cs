using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;

namespace PacManBot.Extensions
{
    public static class CollectionExtensions
    {
        /// <summary>Removes and returns the last element of a list.</summary>
        public static T Pop<T>(this IList<T> list)
        {
            var popped = list[list.Count - 1];
            list.RemoveAt(list.Count - 1);
            return popped;
        }


        /// <summary>Removes and returns up to <paramref name="amount"/> elements from the end of a list.</summary>
        public static List<T> PopRange<T>(this IList<T> list, int amount)
        {
            var popped = new List<T>();
            for (int i = 0; i < amount && i < list.Count; i++) popped.Add(list.Pop());
            return popped;
        }


        /// <summary>Swaps two elements in a list</summary>
        public static void Swap<T>(this IList<T> list, int index1, int index2)
        {
            T temp = list[index1];
            list[index1] = list[index2];
            list[index2] = temp;
        }


        /// <summary>If <paramref name="index"/> is not within the bounds of the list,
        /// returns a new value that has been looped around the list coming out from the other end.</summary>
        public static int LoopedIndex<T>(this IList<T> list, int index)
        {
            if (list.Count == 0) throw new InvalidOperationException("List contains no elements");

            index %= list.Count;
            if (index < 0) index += list.Count;
            return index;
        }


        /// <summary>Returns an element from a list at a given index. If the index is not
        /// within the bounds of the array, it is looped around the list, coming out from the other end.</summary>
        public static T GetAtLooped<T>(this IList<T> list, int loopedIndex)
        {
            return list[LoopedIndex(list, loopedIndex)];
        }


        /// <summary>Sets a list's element at a given index. If the index is not
        /// within the bounds of the array, it is looped around the list, coming out from the other end.</summary>
        public static void SetAtLooped<T>(this IList<T> list, int loopedIndex, T value)
        {
            list[list.LoopedIndex(loopedIndex)] = value;
        }


        /// <summary>Shifts all the elements of a list by the given amount.</summary>
        public static void Shift<T>(this IList<T> list, int amount)
        {
            var old = list.ToArray();
            for (int i = 0; i < list.Count; i++)
            {
                list[i] = old.GetAtLooped(i + amount);
            }
        }


        /// <summary>Splits a list into sublists of the specified maximum size.</summary>
        public static List<List<T>> Split<T>(this List<T> list, int size)
        {
            var lists = new List<List<T>>();
            for (int i = 0; i < list.Count; i += size)
            {
                lists.Add(list.GetRange(i, Math.Min(size, list.Count - i)));
            }
            return lists;
        }


        /// <summary>Sorts and returns the same list.</summary>
        public static List<T> Sorted<T>(this List<T> list)
        {
            list.Sort();
            return list;
        }


        /// <summary>Reverses and returns the same list.</summary>
        public static List<T> Reversed<T>(this List<T> list)
        {
            list.Reverse();
            return list;
        }


        /// <summary>Returns a new array that is the concatenation of all the provided arrays.</summary>
        public static T[] Concatenate<T>(this T[] first, params T[][] others)
        {
            if (first == null) throw new ArgumentNullException(nameof(first));
            if (others == null || others.Any(x => x == null)) throw new ArgumentNullException(nameof(others));

            var newArray = new T[first.Length + others.Select(x => x.Length).Sum()];
            Array.Copy(first, 0, newArray, 0, first.Length);

            int startIndex = first.Length;
            foreach (var array in others)
            {
                Array.Copy(array, 0, newArray, startIndex, array.Length);
                startIndex += array.Length;
            }

            return newArray;
        }


        /// <summary>Returns a new array that is the concatenation of the first array and
        /// an array of the provided elements.</summary>
        public static T[] Concatenate<T>(this T[] first, params T[] second)
        {
            if (first == null) throw new ArgumentNullException(nameof(first));
            if (second == null) throw new ArgumentNullException(nameof(second));

            return first.Concatenate(new[] { second });
        }


        /// <summary>Returns a <see cref="Nullable{T}"/> that has the value of the first element 
        /// in the list that satisfies the specified condition if there is one, otherwise it has no value.</summary>
        public static T? FirstOrNull<T>(this IEnumerable<T> list, Func<T, bool> predicate) where T : struct
        {
            try
            {
                return list.First(predicate);
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }


        /// <summary>Attempts to remove the value that has the specified key from the 
        /// <see cref="System.Collections.Concurrent.ConcurrentDictionary{TKey, TValue}"/>.</summary>
        public static bool TryRemove<TKey, TValue>(this ConcurrentDictionary<TKey, TValue> dict, TKey key)
        {
            return dict.TryRemove(key, out _);
        }


        /// <summary>Creates a <see cref="Dictionary{TKey, TValue}"/> from a collection of 
        /// <see cref="KeyValuePair{TKey, TValue}"/>s.</summary>
        public static Dictionary<TKey, TValue> AsDictionary<TKey, TValue>(this IEnumerable<KeyValuePair<TKey, TValue>> pairs)
        {
            return pairs.ToDictionary(x => x.Key, x => x.Value);
        }


        /// <summary>Returns a read-only wrapper for the given dictionary.</summary>
        public static ReadOnlyDictionary<TKey, TValue> AsReadOnly<TKey, TValue>(this Dictionary<TKey, TValue> dictionary)
        {
            return new ReadOnlyDictionary<TKey, TValue>(dictionary);
        }
    }
}
