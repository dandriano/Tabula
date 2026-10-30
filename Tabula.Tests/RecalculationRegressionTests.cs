using Tabula.Cells;
using Tabula.Values;
using Xunit;

namespace Tabula.Tests;

public sealed class RecalculationRegressionTests
{
    [Fact]
    public void SupportSetsInvalidateTransitiveDependents()
    {
        var workbook = new Workbook();
        var sheet = new Sheet(workbook, "Data", 8, 8);
        sheet[0, 0] = Cell.Parse("1", workbook, 0, 0);
        sheet[1, 0] = Cell.Parse("=A1+1", workbook, 1, 0);
        sheet[2, 0] = Cell.Parse("=B1+1", workbook, 2, 0);
        workbook.RecalculateFullRebuild();

        sheet[0, 0] = Cell.Parse("10", workbook, 0, 0);
        workbook.Recalculate();

        Assert.Equal(12, Number(sheet[2, 0].Eval(sheet, 2, 0)));
    }

    [Fact]
    public void ReplacingFormulaRebuildsItsSupportSetBeforeIncrementalRecalculation()
    {
        var workbook = new Workbook();
        var sheet = new Sheet(workbook, "Data", 8, 8);
        sheet[0, 0] = Cell.Parse("1", workbook, 0, 0);
        sheet[1, 0] = Cell.Parse("10", workbook, 1, 0);
        sheet[2, 0] = Cell.Parse("=A1", workbook, 2, 0);
        workbook.RecalculateFullRebuild();

        sheet[2, 0] = Cell.Parse("=B1", workbook, 2, 0);
        workbook.Recalculate();
        Assert.Equal(10, Number(sheet[2, 0].Eval(sheet, 2, 0)));

        sheet[0, 0] = Cell.Parse("2", workbook, 0, 0);
        workbook.Recalculate();
        Assert.Equal(10, Number(sheet[2, 0].Eval(sheet, 2, 0)));

        sheet[1, 0] = Cell.Parse("20", workbook, 1, 0);
        workbook.Recalculate();
        Assert.Equal(20, Number(sheet[2, 0].Eval(sheet, 2, 0)));

        workbook.RecalculateFullRebuild();
        Assert.Equal(20, Number(sheet[2, 0].Eval(sheet, 2, 0)));
    }

    [Fact]
    public void AddingFormulaToBlankCellAndReplacingItWithConstantRemainCorrect()
    {
        var workbook = new Workbook();
        var sheet = new Sheet(workbook, "Data", 8, 8);
        sheet[0, 0] = Cell.Parse("1", workbook, 0, 0);
        workbook.RecalculateFullRebuild();

        sheet[1, 0] = Cell.Parse("=A1+1", workbook, 1, 0);
        workbook.Recalculate();
        Assert.Equal(2, Number(sheet[1, 0].Eval(sheet, 1, 0)));

        sheet[0, 0] = Cell.Parse("5", workbook, 0, 0);
        workbook.Recalculate();
        Assert.Equal(6, Number(sheet[1, 0].Eval(sheet, 1, 0)));

        sheet[1, 0] = Cell.Parse("11", workbook, 1, 0);
        workbook.Recalculate();
        Assert.Equal(11, Number(sheet[1, 0].Eval(sheet, 1, 0)));

        sheet[0, 0] = Cell.Parse("7", workbook, 0, 0);
        workbook.Recalculate();
        Assert.Equal(11, Number(sheet[1, 0].Eval(sheet, 1, 0)));

        workbook.RecalculateFullRebuild();
        Assert.Equal(11, Number(sheet[1, 0].Eval(sheet, 1, 0)));
    }

    [Fact]
    public void RecalculatesVolatileFormulas()
    {
        var workbook = new Workbook();
        var sheet = new Sheet(workbook, "Data", 8, 8);
        sheet[0, 0] = Cell.Parse("=RAND()", workbook, 0, 0);
        workbook.RecalculateFullRebuild();
        double first = Number(sheet[0, 0].Eval(sheet, 0, 0));

        workbook.Recalculate();

        Assert.NotEqual(first, Number(sheet[0, 0].Eval(sheet, 0, 0)));
    }

    private static double Number(Value value) => Assert.IsType<NumberValue>(value).Value;
}
