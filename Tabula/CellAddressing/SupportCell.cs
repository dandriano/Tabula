using System;

namespace Tabula.CellAddressing;

/// <summary>
/// A SupportCell is a single supported cell.
/// </summary>
public class SupportCell : SupportRange
{
    private readonly Sheet _sheet;
    private readonly int _col, _row;

    public SupportCell(Sheet sheet, int col, int row)
    {
        _sheet = sheet;
        _col = col;
        _row = row;
    }

    public override bool RemoveCell(SupportSet set, Sheet sheet, int col, int row)
    {
        return Contains(sheet, col, row);
    }

    public override void ForEachSupported(Action<Sheet, int, int> act)
    {
        act(_sheet, _col, _row);
    }

    public override bool Contains(Sheet sheet, int col, int row)
    {
        return _sheet == sheet && _col == col && _row == row;
    }

    public override int Count => 1;
}