using Tabula.CellAddressing;


namespace Tabula.Values;

/// <summary>
/// An ArrayExplicit is a view of a materialized array of Values, typically
/// resulting from evaluating TRANSPOSE or other array-valued functions.
/// </summary>
public class ArrayExplicit : ArrayValue
{
    public CellAddr UlCa { get; }
    public CellAddr LrCa { get; }
    public Value[,] Values { get; }
    private readonly int _cols, _rows;

    public ArrayExplicit(Value[,] values)
        : this(new CellAddr(0, 0), new CellAddr(values.GetLength(0) - 1, values.GetLength(1) - 1), values)
    {
    }

    public ArrayExplicit(CellAddr ulCa, CellAddr lrCa, Value[,] values)
    {
        UlCa = ulCa;
        LrCa = lrCa;
        Values = values;
        _cols = lrCa.Column - ulCa.Column + 1;
        _rows = lrCa.Row - ulCa.Row + 1;
    }

    public override Value View(CellAddr ulCa, CellAddr lrCa)
    {
        return new ArrayExplicit(ulCa.Offset(UlCa), lrCa.Offset(UlCa), Values);
    }

    public override Value Slice(CellAddr ulCa, CellAddr lrCa)
    {
        return View(ulCa, lrCa);
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
                return Values[c, r];
            }
            else
            {
                return ErrorValue.NaError;
            }
        }
    }

    public override bool Equals(Value v)
    {
        return EqualElements(this, v as ArrayValue);
    }
}
