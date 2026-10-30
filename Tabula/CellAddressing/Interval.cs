using System;

namespace Tabula.CellAddressing;

/// <summary>
/// An Interval is a non-empty integer interval [min...max].
/// </summary>
public readonly record struct Interval
{
    public int Min { get; init; }
    public int Max { get; init; }

    public Interval(int min, int max)
    {
        if (min > max)
            throw new ArgumentException();
        Min = min;
        Max = max;
    }

    public readonly void ForEach(Action<int> act)
    {
        for (int i = Min; i <= Max; i++)
            act(i);
    }

    public readonly bool Contains(int i) => Min <= i && i <= Max;

    public readonly int Length => Max - Min + 1;

    public readonly bool Overlaps(Interval that)
    {
        return Min <= that.Min && that.Min <= Max
               || that.Min <= Min && Min <= that.Max;
    }

    // When the intervals overlap, this is their union:
    public readonly Interval Join(Interval that)
    {
        return new Interval(Math.Min(Min, that.Min), Math.Max(Max, that.Max));
    }

    // When the intervals overlap, this is their intersection:
    public readonly Interval Meet(Interval that)
    {
        return new Interval(Math.Max(Min, that.Min), Math.Min(Max, that.Max));
    }
}
