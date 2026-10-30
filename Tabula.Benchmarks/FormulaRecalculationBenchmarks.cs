using System;
using BenchmarkDotNet.Attributes;
using Tabula.CellAddressing;
using Tabula.Cells;
using Tabula.Types;
using Tabula.Values;

namespace Tabula.Benchmarks;

[MemoryDiagnoser]
[MediumRunJob]
public class FormulaRecalculationBenchmarks
{
    private Workbook _rightwardWorkbook;
    private Sheet _rightwardSheet;
    private Workbook _leftwardWorkbook;
    private Sheet _leftwardSheet;

    [Params(10, 100, 500)]
    public int Rows { get; set; }

    [Params(10, 100, 500)]
    public int Columns { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        (_rightwardWorkbook, _rightwardSheet) = CreateWorkbook(Columns, Rows, rightward: true);
        (_leftwardWorkbook, _leftwardSheet) = CreateWorkbook(Columns, Rows, rightward: false);

        ValidateWorkload(_rightwardWorkbook, _rightwardSheet, rightward: true);
        ValidateWorkload(_leftwardWorkbook, _leftwardSheet, rightward: false);
    }

    [Benchmark]
    public void RecalculateRightwardLayout() => _rightwardWorkbook.RecalculateFull();

    [Benchmark]
    public void RecalculateLeftwardLayout() => _leftwardWorkbook.RecalculateFull();

    private static (Workbook workbook, Sheet sheet) CreateWorkbook(int columns, int rows, bool rightward)
    {
        var workbook = new Workbook();
        var sheet = new Sheet(workbook, rightward ? "Rightward" : "Leftward", columns, rows);

        for (int row = 0; row < rows; row++)
        {
            int displayedRow = row + 1;
            if (rightward)
            {
                for (int col = 0; col < columns - 1; col++)
                    sheet[col, row] = Cell.Parse("=" + CellAddr.ColumnName(col + 1) + displayedRow,
                        workbook, col, row);
                sheet[columns - 1, row] = Cell.Parse(displayedRow.ToString(), workbook, columns - 1, row);
            }
            else
            {
                sheet[0, row] = Cell.Parse(displayedRow.ToString(), workbook, 0, row);
                for (int col = 1; col < columns; col++)
                    sheet[col, row] = Cell.Parse("=" + CellAddr.ColumnName(col - 1) + displayedRow,
                        workbook, col, row);
            }
        }

        return (workbook, sheet);
    }

    private static void ValidateWorkload(Workbook workbook, Sheet sheet, bool rightward)
    {
        var nonemptyCount = 0;
        var formulaCount = 0;
        var formats = new Formats();
        for (int row = 0; row < sheet.Rows; row++)
        {
            for (int col = 0; col < sheet.Cols; col++)
            {
                var cell = sheet[col, row];
                if (cell == null)
                    continue;

                nonemptyCount++;
                if (cell.Show(col, row, formats).StartsWith('='))
                    formulaCount++;
            }
        }

        if (nonemptyCount != sheet.Rows * sheet.Cols
            || formulaCount != sheet.Rows * (sheet.Cols - 1))
            throw new InvalidOperationException("The benchmark workbook does not contain the expected cells and formulas.");

        var constantColumn = rightward ? sheet.Cols - 1 : 0;
        workbook.RecalculateFull();
        for (int row = 0; row < sheet.Rows; row++)
        {
            for (int col = 0; col < sheet.Cols; col++)
            {
                if (sheet[col, row].Eval(sheet, col, row) is not NumberValue value
                    || value.Value != row + 1)
                    throw new InvalidOperationException("A row formula did not resolve to that row's constant.");
            }

            if (sheet[constantColumn, row].Show(constantColumn, row, formats) != (row + 1).ToString())
                throw new InvalidOperationException("A row does not contain its expected constant.");
        }

        if (sheet[constantColumn, 0].Show(constantColumn, 0, formats) != "1"
            || sheet[constantColumn, sheet.Rows - 1].Show(constantColumn, sheet.Rows - 1, formats)
                != sheet.Rows.ToString())
            throw new InvalidOperationException("The benchmark constants do not match the row numbers.");
    }
}
