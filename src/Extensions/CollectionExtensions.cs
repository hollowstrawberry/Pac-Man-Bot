using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using PacManBot.Utils;

namespace PacManBot.Extensions
{
    public static class CollectionExtensions
    {
        /// <summary>Fluid method that joins the members of a collection using the specified separator between them.</summary>
        public static string JoinString<T>(this IEnumerable<T> values, string separator = "")
        {
            return string.Join(separator, values);
        }


        /// <summary>Returns a <see cref="Nullable{T}"/> that has the value of the first element 
        /// that satisfies the specified condition if there is one, otherwise it has no value.</summary>
        public static T? FirstOrNull<T>(this IEnumerable<T> source, Func<T, bool> predicate) where T : struct
        {
            foreach (var item in source)
            {
                if (predicate(item)) return item;
            }
            return null;
        }


        /// <summary>
        /// Returns a Python-like list slice that is between the specified boundaries and takes elements by the specified step.
        /// </summary>
        /// <param name="start">The starting index of the slice. Loops around if negative.</param>
        /// <param name="stop">The index the slice goes up to, excluding itself. Loops around if negative.</param>
        /// <param name="step">The increment between each element index. Traverses backwards if negative.</param>
        public static IEnumerable<T> Slice<T>(this IEnumerable<T> list, int? start = null, int? stop = null, int step = 1)
        {
            if (step == 0) throw new ArgumentException("The step of the slice cannot be zero.", nameof(step));

            int count = list.Count();
            if (count == 0) return list;

            if (start == null) start = step > 0 ? 0 : count - 1;
            else if (start < 0) start = count + start;

            if (stop == null) stop = step > 0 ? count : -1;
            else if (stop < 0) stop = count + stop;

            if (step < 0)
            {
                list = list.Reverse();
                start = count - 1 - Math.Min(count - 1, start.Value); // Also reverses indices
                stop = count - 1 - stop.Value;
            }

            return list.Skip(start.Value).Take(stop.Value - start.Value).Where((_, i) => i % step == 0);
        }


        /// <summary>Returns the index of an element contained in a list if it is found, otherwise returns -1.</summary>
        public static int IndexOf<T>(this IReadOnlyList<T> list, T element) // IList doesn't implement IndexOf for some reason
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Equals(element)) return i;
            }
            return -1;
        }


        /// <summary>Removes and returns the last element of a list.</summary>
        public static T Pop<T>(this IList<T> list)
        {
            var popped = list[list.Count - 1];
            list.RemoveAt(list.Count - 1);
            return popped;
        }


        /// <summary>Removes and returns up to the specified amount of elements from the end of a list.</summary>
        public static List<T> PopRange<T>(this IList<T> list, int amount)
        {
            var popped = new List<T>();
            for (int i = 0; i < amount && i < list.Count; i++) popped.Add(list.Pop());
            return popped;
        }


        /// <summary>Swaps two elements in a list.</summary>
        public static void Swap<T>(this IList<T> list, int index1, int index2)
        {
            T temp = list[index1];
            list[index1] = list[index2];
            list[index2] = temp;
        }


        /// <summary>Shifts all the elements of a list by the given amount.</summary>
        public static List<T> Shift<T>(this List<T> list, int amount)
        {
            var old = new LoopedList<T>(list).Copy();
            for (int i = 0; i < list.Count; i++)
            {
                list[i] = old[i + amount];
            }
            return list;
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
