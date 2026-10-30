using Tabula.Cells;
using Tabula.Values;
using Xunit;

namespace Tabula.Tests;

public sealed class FormulaLayoutTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EachRowFormulaChainResolvesToItsConstant(bool rightward)
    {
        const int rows = 24;
        const int cols = 24;
        var workbook = new Workbook();
        var sheet = new Sheet(workbook, "Layout", cols, rows);

        for (int row = 0; row < rows; row++)
        {
            int displayedRow = row + 1;
            if (rightward)
            {
                for (int col = 0; col < cols - 1; col++)
                    sheet[col, row] = Cell.Parse("=" + CellAddressing.CellAddr.ColumnName(col + 1) + displayedRow,
                        workbook, col, row);
                sheet[4, row] = Cell.Parse(displayedRow.ToString(), workbook, cols, row);
            }
            else
            {
                sheet[0, row] = Cell.Parse(displayedRow.ToString(), workbook, 0, row);
                for (int col = 1; col < cols; col++)
                    sheet[col, row] = Cell.Parse("=" + CellAddressing.CellAddr.ColumnName(col - 1) + displayedRow,
                        workbook, col, row);
            }
        }

        workbook.RecalculateFull();

        for (int row = 0; row < rows; row++)
            for (int col = 0; col < 5; col++)
                Assert.Equal(row + 1, Assert.IsType<NumberValue>(sheet[col, row].Eval(sheet, col, row)).Value);
    }
}
