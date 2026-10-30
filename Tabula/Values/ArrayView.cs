using System;
using Tabula.CellAddressing;
using Tabula.Cells;

namespace Tabula.Values;

/// <summary>
/// An ArrayView is a rectangular view of a sheet, the
/// result of evaluating a CellArea expression.  Accessing an 
/// element of an ArrayView may cause a cell to be evaluated.
/// </summary>
public class ArrayView : ArrayValue, IEquatable<ArrayView>
{
    public CellAddr UlCa { get; }
    public CellAddr LrCa { get; }
    public Sheet Sheet { get; }
    private readonly int _cols, _rows;

    private ArrayView(CellAddr ulCa, CellAddr lrCa, Sheet sheet)
    {
        Sheet = sheet;
        UlCa = ulCa;
        LrCa = lrCa;
        _cols = lrCa.Column - ulCa.Column + 1;
        _rows = lrCa.Row - ulCa.Row + 1;
    }

    public static ArrayView Make(CellAddr ulCa, CellAddr lrCa, Sheet sheet)
    {
        CellAddr.NormalizeArea(ulCa, lrCa, out ulCa, out lrCa);
        return new ArrayView(ulCa, lrCa, sheet);
    }

    public override int Cols
    {
        get
        {
            return _cols;
        }
    }

    public override int Rows
    {
        get
        {
            return _rows;
        }
    }

    // Evaluate and get value at offset [col, row], 0-based
    public override Value this[int col, int row]
    {
        get
        {
            if (0 <= col && col < Cols && 0 <= row && row < Rows)
            {
                int c = UlCa.Column + col, r = UlCa.Row + row;
                var cell = Sheet[c, r];
                if (cell == null)
                    return null;

                return cell.Eval(Sheet, c, r);
            }
            else
            {
                return ErrorValue.NaError;
            }
        }
    }

    public override Value View(CellAddr ulCa, CellAddr lrCa)
    {
        return Make(ulCa.Offset(UlCa), lrCa.Offset(UlCa), Sheet);
    }

    public override Value Slice(CellAddr ulCa, CellAddr lrCa)
    {
        return new ArrayView(ulCa.Offset(UlCa), lrCa.Offset(UlCa), Sheet);
    }

    public void Apply(Action<FullCellAddr> act)
    {
        int col0 = UlCa.Column, row0 = UlCa.Row;
        for (int c = 0; c < _cols; c++)
            for (int r = 0; r < _rows; r++)
                act(new FullCellAddr(Sheet, col0 + c, row0 + r));
    }

    public override bool Equals(Value v)
    {
        return v is ArrayView && Equals(v as ArrayView)
               || EqualElements(this, v as ArrayValue);
    }

    // Compare the source range without reading the cells in that range.
    public bool Equals(ArrayView other)
    {
        return Sheet == other.Sheet && UlCa.Equals(other.UlCa) && LrCa.Equals(other.LrCa);
    }

    public override bool Equals(object o)
    {
        return o is ArrayView view && Equals(view);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Sheet, UlCa, LrCa);
    }
}
