using System;
using System.Collections.Generic;

namespace Tabula.Types;

/// <summary>
/// Machinery to store objects of type T for later access via an integer index.
/// </summary>
/// <typeparam name="T">The type of item stored in the array</typeparam>
internal sealed class ValueTable<T> where T : IEquatable<T>
{
    private readonly IDictionary<T, int> _dict = new Dictionary<T, int>();
    private readonly IList<T> _array = new List<T>();

    public int GetIndex(T x)
    {
        int index;
        if (!_dict.TryGetValue(x, out index))
        {
            index = _array.Count;
            _array.Add(x);
            _dict.Add(x, index);
        }
        return index;
    }

    public T this[int index] => _array[index];
}
