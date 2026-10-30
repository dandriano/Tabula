using System;
using System.Collections.Generic;
using System.Linq;

namespace Tabula.Types;

public class HashList<T> : IEnumerable<T> where T : IEquatable<T>
{
    // Invariants: No duplicates in seq; seq and set have the same 
    // sets of items and the same number of items.
    private readonly List<T> _sequence = new List<T>();
    private readonly HashSet<T> _set = new HashSet<T>();

    public bool Contains(T item)
    {
        return _set.Contains(item);
    }

    public int Count => _sequence.Count;

    public bool Add(T item)
    {
        if (_set.Contains(item))
        {
            return false;
        }
        else
        {
            _sequence.Add(item);
            _set.Add(item);
            return true;
        }
    }

    public void AddAll(IEnumerable<T> xs)
    {
        foreach (T x in xs)
        {
            Add(x);
        }
    }

    public static HashList<T> Union(HashList<T> ha1, HashList<T> ha2)
    {
        HashList<T> result = new HashList<T>();
        result.AddAll(ha1);
        result.AddAll(ha2);
        return result;
    }

    public static HashList<T> Intersection(HashList<T> ha1, HashList<T> ha2)
    {
        HashList<T> result = new HashList<T>();
        foreach (var x in ha1.Where(ha2.Contains))
        {
            result.Add(x);
        }
        return result;
    }

    public static HashList<T> Difference(HashList<T> ha1, HashList<T> ha2)
    {
        HashList<T> result = new HashList<T>();
        foreach (T x in ha1.Where(x => !ha2.Contains(x)))
        {
            result.Add(x);
        }
        return result;
    }

    public bool UnsequencedEquals(HashList<T> that)
    {
        return Count == that.Count && _sequence.All(x => that._set.Contains(x));
    }

    public T[] ToArray()
    {
        return _sequence.ToArray();
    }

    public IEnumerator<T> GetEnumerator()
    {
        return _sequence.GetEnumerator();
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
