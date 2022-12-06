using System;
using System.Collections;
using System.Collections.Generic;

namespace Splitracker.Web.Shared;

static class SplitrackerEnumerableExtensions
{
    public readonly struct Row<T> : IEnumerable<T>, IReadOnlyList<T>
    {
        readonly T[,] array;
        readonly int rowIndex;

        public Row(T[,] array, int rowIndex)
        {
            this.array = array;
            this.rowIndex = rowIndex;
        }

        public IEnumerator<T> GetEnumerator()
        {
            for (var i = 0; i < array.GetLength(1); i++)
                yield return array[rowIndex, i];
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public int Count => array.GetLength(1);

        public T this[int index] => array[rowIndex, index];
    }

    public static IEnumerable<Row<T>> ByRows<T>(this T[,] array) {
        for (var i = 0; i < array.GetLength(0); i++)
            yield return new(array, i);
    }
    
    public static IEnumerable<(T Item, int Index)> Enumerated<T>(this IEnumerable<T> input)
    {
        var index = 0;
        foreach (var item in input)
            yield return (item, index++);
    }

    /// <summary>
    /// Sequence that starts at <c>xs[startIndex]</c> and then alternates between earlier and later elements.
    /// In other words: xs[startIndex], xs[startIndex - 1], xs[startIndex + 1], xs[startIndex - 2], xs[startIndex + 2], ...
    /// </summary>
    /// <remarks><paramref name="startIndex"/> gets clamped to the range of valid indexes.
    /// An empty input sequence <paramref name="xs"/> results in an empty sequence.</remarks>
    /// <param name="xs">The sequence</param>
    /// <param name="startIndex">The starting point (the element at this index is returned first)</param>
    public static IEnumerable<T> RadialSearch<T>(this IReadOnlyList<T> xs, int startIndex)
    {
        if (xs.Count == 0)
            yield break;

        startIndex = Math.Clamp(startIndex, 0, xs.Count - 1);
        yield return xs[startIndex];

        var step = 1;
        while (true)
        {
            var left = startIndex - step;
            if (left >= 0)
                yield return xs[left];

            var right = startIndex + step;
            if (right < xs.Count)
                yield return xs[right];

            step++;
        }
    }
}