using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;

namespace PacManBot.Extensions
{
    public static class CollectionExtensions
    {
        public static T Pop<T>(this IList<T> list)
        {
            var popped = list[list.Count - 1];
            list.RemoveAt(list.Count - 1);
            return popped;
        }


        public static List<T> PopRange<T>(this IList<T> list, int amount)
        {
            var popped = new List<T>();
            for (int i = 0; i < amount && i < list.Count; i++) popped.Add(list.Pop());
            return popped;
        }


        public static void Swap<T>(this IList<T> list, int index1, int index2)
        {
            T temp = list[index1];
            list[index1] = list[index2];
            list[index2] = temp;
        }


        public static int LoopedIndex<T>(this IList<T> list, int index)
        {
            while (index >= list.Count) index -= list.Count;
            while (index < 0) index += list.Count;
            return index;
        }


        public static void Shift<T>(this IList<T> list, int amount)
        {
            var old = list.ToArray();
            for (int i = 0; i < list.Count; i++)
            {
                list[i] = old[old.LoopedIndex(i + amount)];
            }
        }


        public static List<List<T>> Split<T>(this List<T> list, int size)
        {
            var lists = new List<List<T>>();
            for (int i = 0; i < list.Count; i += size)
            {
                lists.Add(list.GetRange(i, Math.Min(size, list.Count - i)));
            }
            return lists;
        }


        public static List<T> Sorted<T>(this List<T> list)
        {
            list.Sort();
            return list;
        }


        public static List<T> Reversed<T>(this List<T> list)
        {
            list.Reverse();
            return list;
        }


        public static T[] Concatenate<T>(this T[] first, params T[][] others)
        {
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


        public static T[] Concatenate<T>(this T[] first, params T[] second)
        {
            return first.Concatenate(new[] { second });
        }


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


        public static bool TryRemove<TKey, TValue>(this ConcurrentDictionary<TKey, TValue> dict, TKey key)
        {
            return dict.TryRemove(key, out _);
        }


        public static Dictionary<TKey, TValue> AsDictionary<TKey, TValue>(this IEnumerable<KeyValuePair<TKey, TValue>> pairs)
        {
            return pairs.ToDictionary(x => x.Key, x => x.Value);
        }


        public static IReadOnlyDictionary<TKey, TValue> AsReadOnly<TKey, TValue>(this Dictionary<TKey, TValue> dictionary)
        {
            return new ReadOnlyDictionary<TKey, TValue>(dictionary);
        }
    }
}
