using System.Collections;
using System.Collections.Generic;

namespace Splitracker.Web.Shared;

static class TwoDimensionalArrayExtensions
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
}