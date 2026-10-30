using System;

namespace Tabula.CellAddressing;

/// <summary>
/// A SupportRange is a single supported cell or a supported cell area on a sheet.
/// </summary>
public abstract class SupportRange
{
    public static SupportRange Make(Sheet sheet, Interval colInt, Interval rowInt)
    {
        if (colInt.Min == colInt.Max && rowInt.Min == rowInt.Max)
            return new SupportCell(sheet, colInt.Min, rowInt.Min);
        else
            return new SupportArea(sheet, colInt, rowInt);
    }

    // Remove cell sheet[col,row] from given support set, possibly adding smaller
    // support ranges at end of supportSet; if so return true
    public abstract bool RemoveCell(SupportSet set, Sheet sheet, int col, int row);

    public abstract void ForEachSupported(Action<Sheet, int, int> act);

    public abstract bool Contains(Sheet sheet, int col, int row);

    public abstract int Count { get; }
}