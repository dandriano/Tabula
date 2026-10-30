using System;
using System.Collections.Generic;

namespace Tabula.Types;

/// <summary>
/// Machinery to cache the creation of objects of type U, when created 
/// from objects of type T, and for later access via an integer index.
/// </summary>
/// <typeparam name="T">The type of key, typically String</typeparam>
/// <typeparam name="U">The type of resulting cached item</typeparam>
internal sealed class ValueCache<T, U> where T : IEquatable<T>
{
    // Assumes: function make is monogenic
    // Invariant: array[dict[x]].Equals(make(x))
    private readonly IDictionary<T, int> _dict = new Dictionary<T, int>();
    private readonly IList<U> _array = new List<U>();
    private readonly Func<int, T, U> _make;

    public ValueCache(Func<int, T, U> make)
    {
        _make = make;
    }

    public int GetIndex(T x)
    {
        int index;
        if (!_dict.TryGetValue(x, out index))
        {
            index = _array.Count;
            _dict.Add(x, index);
            _array.Add(_make(index, x));
        }
        return index;
    }

    public U this[int index] => _array[index];
}
