using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace PacManBot.Utils
{
    public static class CollectionExtensions
    {
        public static T Last<T>(this IList<T> list)
        {
            return list[list.Count - 1];
        }


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
            T[] newArray = new T[first.Length + others.Select(x => x.Length).Sum()];
            Array.Copy(first, 0, newArray, 0, first.Length);

            int startIndex = first.Length;
            foreach (var array in others)
            {
                Array.Copy(array, 0, newArray, startIndex, array.Length);
                startIndex += array.Length;
            }

            return newArray;
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
