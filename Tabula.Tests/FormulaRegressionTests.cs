using Tabula.Cells;
using Tabula.Types;
using Tabula.Values;
using Xunit;

namespace Tabula.Tests;

public sealed class FormulaRegressionTests
{
    [Fact]
    public void EvaluatesArithmeticAndOperators()
    {
        var (workbook, sheet, _, _) = CreateWorkbook();

        Assert.Equal(7, Number(Evaluate(sheet, 5, 0, "=1+2*3")));
        Assert.Equal(9, Number(Evaluate(sheet, 6, 0, "=(1+2)*3")));
        Assert.Equal(.2, Number(Evaluate(sheet, 7, 0, "=20%")));
        Assert.Equal("tabula", Text(Evaluate(sheet, 8, 0, "=\"tab\"&\"ula\"")));
        Assert.Equal(1, Number(Evaluate(sheet, 7, 4, "=1<2")));
    }

    [Fact]
    public void EvaluatesCellReferencesAndRanges()
    {
        var (_, sheet, _, _) = CreateWorkbook();

        Assert.Equal(3, Number(Evaluate(sheet, 3, 3, "=A1+$A$2")));
        Assert.Equal(9, Number(Evaluate(sheet, 3, 4, "=Other!A1")));
        Assert.Equal(4, Number(Evaluate(sheet, 4, 4, "='Sheet Two'!A1")));
        Assert.Equal(6, Number(Evaluate(sheet, 5, 4, "=SUM(A1:A3)")));
        Assert.Equal(19, Number(Evaluate(sheet, 6, 4, "=SUM(Other!A1:A2)")));
    }

    [Fact]
    public void ParsesR1C1ReferencesAsCanonicalReferences()
    {
        var workbook = new Workbook();
        var sheet = new Sheet(workbook, "Main", 8, 8);
        var other = new Sheet(workbook, "Other", 8, 8);
        var r1c1Named = new Sheet(workbook, "R1C1", 8, 8);
        sheet[0, 0] = Cell.Parse("1", workbook, 0, 0);
        sheet[1, 0] = Cell.Parse("2", workbook, 1, 0);
        sheet[0, 1] = Cell.Parse("3", workbook, 0, 1);
        sheet[1, 1] = Cell.Parse("4", workbook, 1, 1);
        other[0, 0] = Cell.Parse("9", workbook, 0, 0);
        r1c1Named[0, 0] = Cell.Parse("11", workbook, 0, 0);

        Assert.Equal(1, Number(Evaluate(sheet, 2, 1, "=R1C1", ReferenceStyle.R1C1)));
        Assert.Equal(2, Number(Evaluate(sheet, 2, 1, "=R1C[-1]", ReferenceStyle.R1C1)));
        Assert.Equal(2, Number(Evaluate(sheet, 2, 1, "=R[-1]C2", ReferenceStyle.R1C1)));
        Assert.Equal(10, Number(Evaluate(sheet, 2, 2, "=SUM(R1C1:R2C2)", ReferenceStyle.R1C1)));
        Assert.Equal(9, Number(Evaluate(sheet, 2, 3, "=Other!R1C1", ReferenceStyle.R1C1)));
        var quotedSheetValue = Evaluate(sheet, 3, 2, "='R1C1'!R1C1", ReferenceStyle.R1C1);
        Assert.True(quotedSheetValue is NumberValue, quotedSheetValue?.ToString());
        Assert.Equal(11, ((NumberValue)quotedSheetValue).Value);
        Assert.Equal("R1C1", Text(Evaluate(sheet, 3, 3, "=\"R1C1\"")));

        var mixedReference = Cell.Parse("=R[-1]C[+2]", workbook, 3, 3, ReferenceStyle.R1C1);
        Assert.Equal("=C[+2]R[-1]", mixedReference.Show(3, 3,
            new Formats { RefFmt = Formats.RefType.C0R0 }));
        var copied = mixedReference.CloneCell(5, 5);
        Assert.Equal("=C[+2]R[-1]", copied.Show(5, 5,
            new Formats { RefFmt = Formats.RefType.C0R0 }));
    }

    [Fact]
    public void AdjustsRelativeR1C1ReferencesWhenRowsAreInserted()
    {
        var workbook = new Workbook();
        var sheet = new Sheet(workbook, "Main", 8, 8);
        sheet[1, 0] = Cell.Parse("2", workbook, 1, 0);
        var formula = Cell.Parse("=R[-2]C[-1]", workbook, 2, 2, ReferenceStyle.R1C1);
        formula.MarkDirty();
        sheet[2, 2] = formula;

        sheet.InsertRowCols(1, 1, doRows: true);

        Assert.Equal("=C[-1]R[-3]", sheet[2, 3].Show(2, 3,
            new Formats { RefFmt = Formats.RefType.C0R0 }));
        Assert.Equal(2, Number(sheet[2, 3].Eval(sheet, 2, 3)));
    }

    [Fact]
    public void AdjustsRelativeR1C1ReferencesWhenColumnsAreInserted()
    {
        var workbook = new Workbook();
        var sheet = new Sheet(workbook, "Main", 8, 8);
        sheet[0, 1] = Cell.Parse("12", workbook, 0, 1);
        var formula = Cell.Parse("=R[-1]C[-2]", workbook, 2, 2, ReferenceStyle.R1C1);
        formula.MarkDirty();
        sheet[2, 2] = formula;

        sheet.InsertRowCols(1, 1, doRows: false);

        Assert.Equal("=C[-3]R[-1]", sheet[3, 2].Show(3, 2,
            new Formats { RefFmt = Formats.RefType.C0R0 }));
        Assert.Equal(12, Number(sheet[3, 2].Eval(sheet, 3, 2)));
    }

    [Fact]
    public void MovesRelativeReferencesWithTheirOriginalTargets()
    {
        var workbook = new Workbook();
        var sheet = new Sheet(workbook, "Main", 8, 8);
        sheet[0, 0] = Cell.Parse("7", workbook, 0, 0);
        var formula = Cell.Parse("=R[-2]C[-2]", workbook, 2, 2, ReferenceStyle.R1C1);

        var moved = formula.MoveContents(2, 2);
        moved.MarkDirty();

        Assert.Equal("=C[-4]R[-4]", moved.Show(4, 4,
            new Formats { RefFmt = Formats.RefType.C0R0 }));
        Assert.Equal(7, Number(moved.Eval(sheet, 4, 4)));
    }

    [Fact]
    public void EvaluatesCountingTextAndLookupFunctions()
    {
        var (_, sheet, _, _) = CreateWorkbook();

        Assert.Equal(3, Number(Evaluate(sheet, 5, 1, "=COUNT(A1:B3)")));
        Assert.Equal(4, Number(Evaluate(sheet, 5, 2, "=COUNTA(A1:B3)")));
        Assert.Equal("ab", Text(Evaluate(sheet, 5, 3, "=LEFT(\"abcdef\",2)")));
        Assert.Equal("ef", Text(Evaluate(sheet, 6, 3, "=RIGHT(\"abcdef\",2)")));
        Assert.Equal("bcd", Text(Evaluate(sheet, 7, 3, "=MID(\"abcdef\",2,3)")));
        Assert.Equal(6, Number(Evaluate(sheet, 8, 3, "=LEN(\"abcdef\")")));
        Assert.Equal(2, Number(Evaluate(sheet, 5, 4, "=MATCH(2,A1:A3,0)")));
        Assert.Equal(2, Number(Evaluate(sheet, 6, 7, "=MATCH(2.5,A1:A3)")));
        Assert.Equal(0, Number(Evaluate(sheet, 7, 4, "=VLOOKUP(2,A1:B3,2,FALSE)")));
        Assert.Equal("pear", Text(Evaluate(sheet, 8, 4, "=VLOOKUP(1,A1:B3,2,FALSE)")));
    }

    [Fact]
    public void ReturnsExcelErrorsAndPreservesLazyIfEvaluation()
    {
        var (_, sheet, _, _) = CreateWorkbook();

        AssertError(Evaluate(sheet, 6, 6, "=MATCH(5,A1:A3,0)"), "#N/A");
        AssertError(Evaluate(sheet, 9, 4, "=VLOOKUP(8,A1:B3,2,FALSE)"), "#N/A");
        AssertError(Evaluate(sheet, 10, 4, "=NA()"), "#N/A");
        Assert.Equal("safe", Text(Evaluate(sheet, 5, 5, "=IFERROR(1/0,\"safe\")")));
        AssertError(Evaluate(sheet, 6, 5, "=1/0"), "#DIV/0!");
        Assert.Equal(9, Number(Evaluate(sheet, 7, 5, "=IF(0,1/0,9)")));
        AssertError(Evaluate(sheet, 8, 5, "=UNSUPPORTED_FUNCTION(1)"), "#NAME?");
        Assert.Null(Cell.Parse("=SUM(", sheet.Workbook, 11, 6));

        var unsupported = Evaluate(sheet, 9, 5, "=SomeNamedRange");
        Assert.IsType<ErrorValue>(unsupported);
        Assert.StartsWith("#UNSUPPORTED:", ((ErrorValue)unsupported).Message);
    }

    private static (Workbook workbook, Sheet sheet, Sheet other, Sheet spaced) CreateWorkbook()
    {
        var workbook = new Workbook();
        var sheet = new Sheet(workbook, "Main", 12, 20);
        var other = new Sheet(workbook, "Other", 8, 8);
        var spaced = new Sheet(workbook, "Sheet Two", 8, 8);
        sheet[0, 0] = Cell.Parse("1", workbook, 1, 1);
        sheet[0, 1] = Cell.Parse("2", workbook, 1, 2);
        sheet[0, 2] = Cell.Parse("3", workbook, 1, 3);
        sheet[1, 0] = Cell.Parse("\"pear\"", workbook, 2, 1);
        other[0, 0] = Cell.Parse("9", workbook, 1, 1);
        other[0, 1] = Cell.Parse("10", workbook, 1, 2);
        spaced[0, 0] = Cell.Parse("4", workbook, 1, 1);
        return (workbook, sheet, other, spaced);
    }

    private static Value Evaluate(Sheet sheet, int col, int row, string formula,
                                  ReferenceStyle referenceStyle = ReferenceStyle.A1)
    {
        var cell = Cell.Parse(formula, sheet.Workbook, col, row, referenceStyle);
        Assert.NotNull(cell);
        cell.MarkDirty();
        sheet[col, row] = cell;
        return cell.Eval(sheet, col, row);
    }

    private static double Number(Value value) => Assert.IsType<NumberValue>(value).Value;
    private static string Text(Value value) => Assert.IsType<TextValue>(value).Value;

    private static void AssertError(Value value, string expected)
    {
        Assert.Equal(expected, Assert.IsType<ErrorValue>(value).Message);
    }
}
