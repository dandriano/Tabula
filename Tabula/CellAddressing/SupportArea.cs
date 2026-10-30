using System;
using System.Collections.Generic;

namespace Tabula.CellAddressing;

/// <summary>
/// A SupportArea is a supported  absolute cell area sheet[colInt, rowInt].
/// </summary>
public class SupportArea : SupportRange
{
    private static readonly List<SupportArea> _visited = new List<SupportArea>();
    private static bool _idempotentFor;

    public Interval Column { get; }
    public Interval Row { get; }
    public Sheet Sheet { get; }

    public SupportArea(Sheet sheet, Interval colInt, Interval rowInt)
    {
        Sheet = sheet;
        Column = colInt;
        Row = rowInt;
    }

    public override bool RemoveCell(SupportSet set, Sheet sheet, int col, int row)
    {
        if (Contains(sheet, col, row))
        {
            // To exclude cell at sheet[col, row], split into up to 4 support ranges
            if (Row.Min < row) // North, column above [col,row]
            {
                set.Add(Make(sheet, new Interval(col, col), new Interval(Row.Min, row - 1)));
            }
            if (row < Row.Max) // South, column below [col,row]
            {
                set.Add(Make(sheet, new Interval(col, col), new Interval(row + 1, Row.Max)));
            }
            if (Column.Min < col) // West, block to the left of [col,row]
            {
                set.Add(Make(sheet, new Interval(Column.Min, col - 1), Row));
            }
            if (col < Column.Max) // East, block to the right of [col,row]
            {
                set.Add(Make(sheet, new Interval(col + 1, Column.Max), Row));
            }
            return true;
        }
        else
        {
            return false;
        }
    }

    public static bool IdempotentForeach
    {
        get
        {
            return _idempotentFor;
        }
        set
        {
            _idempotentFor = value;
            _visited.Clear();
        }
    }

    public override void ForEachSupported(Action<Sheet, int, int> act)
    {
        if (IdempotentForeach && Count > _visited.Count + 1)
        {
            for (int i = 0; i < _visited.Count; i++)
            {
                var old = _visited[i];
                if (Overlaps(old))
                {
                    var overlap = Overlap(old);
                    if (overlap.Count == Count)
                    { // contained in old
                        return;
                    }
                    else if (overlap.Count == old.Count)
                    { // contains old
                        _visited[i] = this;
                        ForEachExcept(overlap, act);
                        return;
                    }
                    else if (Column.Equals(old.Column) && Row.Overlaps(old.Row))
                    {
                        _visited[i] = new SupportArea(Sheet, Column, Row.Join(old.Row));
                        ForEachExcept(overlap, act);
                        return;
                    }
                    else if (Row.Equals(old.Row) && Column.Overlaps(old.Column))
                    {
                        _visited[i] = new SupportArea(Sheet, Column.Join(old.Column), Row);
                        ForEachExcept(overlap, act);
                        return;
                    }
                    else
                    { // overlaps, but neither containment nor rectangular union
                        _visited.Add(this);
                        ForEachExcept(overlap, act);
                        return;
                    }
                }
            }
            // Large enough but no existing support set overlaps this one
            _visited.Add(this);
        }
        ForEachInArea(Sheet, Column, Row, act);
    }

    private void ForEachExcept(SupportArea overlap, Action<Sheet, int, int> act)
    {
        if (Row.Min < overlap.Row.Min) // North non-empty, columns above overlap
            ForEachInArea(Sheet, overlap.Column, new Interval(Row.Min, overlap.Row.Min - 1), act);
        if (overlap.Row.Max < Row.Max) // South non-empty, columns below overlap
            ForEachInArea(Sheet, overlap.Column, new Interval(overlap.Row.Max + 1, Row.Max), act);
        if (Column.Min < overlap.Column.Min) // West non-empty, rows left of overlap
            ForEachInArea(Sheet, new Interval(Column.Min, overlap.Column.Min - 1), Row, act);
        if (overlap.Column.Max < Column.Max) // East non-empty, rows right of overlap
            ForEachInArea(Sheet, new Interval(overlap.Column.Max + 1, Column.Max), Row, act);
    }

    private static void ForEachInArea(Sheet sheet,
                                      Interval colInt,
                                      Interval rowInt,
                                      Action<Sheet, int, int> act)
    {
        for (int c = colInt.Min; c <= colInt.Max; c++)
            for (int r = rowInt.Min; r <= rowInt.Max; r++)
                act(sheet, c, r);
    }

    public override bool Contains(Sheet sheet, int col, int row)
    {
        return Sheet == sheet && Column.Contains(col) && Row.Contains(row);
    }

    public override int Count
    {
        get => Column.Length * Row.Length;
    }

    public bool Overlaps(SupportArea that)
    {
        return Sheet == that.Sheet
               && Column.Overlaps(that.Column) && Row.Overlaps(that.Row);
    }

    public SupportArea Overlap(SupportArea that)
    {
        if (!Overlaps(that))
            throw new InvalidOperationException();

        return new SupportArea(Sheet,
                               Column.Meet(that.Column),
                               Row.Meet(that.Row));
    }
}
