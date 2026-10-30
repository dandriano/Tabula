using Tabula.Types;
using Tabula.Values;

namespace Tabula.Cells;

/// <summary>
/// A BlankCell represents an empty cell and displays no text.
/// </summary>
internal sealed class BlankCell : ConstCell
{
    public override Value Eval(Sheet sheet, int col, int row)
    {
        return null;
    }

    public override string Show(int col, int row, Formats fo)
    {
        return "";
    }

    public override Cell CloneCell(int col, int row)
    {
        return new BlankCell();
    }
}
