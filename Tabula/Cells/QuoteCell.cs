using System;
using Tabula.Types;
using Tabula.Values;

namespace Tabula.Cells;

/// <summary>
/// A QuoteCell is a cell containing a single-quoted string constant.
/// </summary>
internal sealed class QuoteCell : ConstCell
{
    public TextValue Value { get; }

    public QuoteCell(string s)
    {
        ArgumentNullException.ThrowIfNull(s);
        Value = TextValue.Make(s); // No interning
    }

    private QuoteCell(QuoteCell cell)
    {
        Value = cell.Value;
    }

    public override Value Eval(Sheet sheet, int col, int row)
    {
        return Value;
    }

    public override string Show(int col, int row, Formats fo)
    {
        return "'" + Value.Value;
    }

    public override Cell CloneCell(int col, int row)
    {
        return new QuoteCell(this);
    }
}
