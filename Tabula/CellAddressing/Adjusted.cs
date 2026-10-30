namespace Tabula.CellAddressing;

/// <summary>
/// An Adjusted<T> represents an adjusted expression or 
/// a relative/absolute ref, for use in method InsertRowCols.
/// </summary>
/// <typeparam name="T">The type of adjusted entity: CalculationNode or RaRef.</typeparam>
public struct Adjusted<T>
{
    public T E { get; }
    public int Upper { get; }
    public bool Same { get; }

    public Adjusted(T e, int upper, bool same)
    {
        E = e;
        Upper = upper;
        Same = same;
    }

    public Adjusted(T e) : this(e, int.MaxValue, true)
    {
    }
}
