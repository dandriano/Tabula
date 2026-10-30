using System;
using Tabula.Types;
using Tabula.Values;

namespace Tabula.Cells;

/// <summary>
/// A NumberCell is a cell containing a floating-point constant.
/// </summary>
internal sealed class NumberCell : ConstCell
{
    public NumberValue Value { get; }

    public NumberCell(double d)
    {
        
        if (double.IsNaN(d) || double.IsInfinity(d))
            throw new ArgumentException();
        Value = (NumberValue)NumberValue.Make(d);
    }

    private NumberCell(NumberCell cell)
    {
        Value = cell.Value;
    }

    public override Value Eval(Sheet sheet, int col, int row)
    {
        return Value;
    }

    public override string Show(int col, int row, Formats fo)
    {
        return Value.Value.ToString();
    }

    public override Cell CloneCell(int col, int row)
    {
        return new NumberCell(this);
    }
}
