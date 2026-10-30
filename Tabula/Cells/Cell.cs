using System;
using System.Collections.Generic;
using System.Globalization;
using Tabula.CellAddressing;
using Tabula.Expressions;
using Tabula.Types;
using Tabula.Values;

namespace Tabula.Cells;

/// <summary>
/// A Cell holds worksheet contents and provides the evaluation, recalculation,
/// dependency, and support-set operations shared by concrete cell types.
/// </summary>
public abstract class Cell : IDepend
{
    // The CellRanges in the support set may overlap; null means empty set
    private SupportSet _set;

    // True if the expression in the cell is volatile
    public abstract bool IsVolatile { get; }

    public abstract Value Eval(Sheet sheet, int col, int row);

    public abstract Cell MoveContents(int deltaCol, int deltaRow);

    public abstract void InsertRowCols(Dictionary<Calculation, Adjusted<Calculation>> adjusted,
                                       Sheet modSheet,
                                       bool thisSheet,
                                       int R,
                                       int N,
                                       int r,
                                       bool doRows);

    public abstract void ResetCellState();

    // Mark the cell dirty, for subsequent evaluation
    public abstract void MarkDirty();

    // Mark cell, dirty if non-empty
    public static void MarkCellDirty(Sheet sheet, int col, int row)
    {
        var cell = sheet[col, row];
        cell?.MarkDirty();
    }

    // Enqueue this cell for evaluation
    public abstract void EnqueueForEvaluation(Sheet sheet, int col, int row);

    // Enqueue the cell at sheet[col, row] for evaluation, if non-null
    public static void EnqueueCellForEvaluation(Sheet sheet, int col, int row)
    {
        var cell = sheet[col, row];
        cell?.EnqueueForEvaluation(sheet, col, row); // Add if not already added, etc
    }

    // Show computed value; overridden in Formula and ArrayFormula to show cached value
    public virtual string ShowValue(Sheet sheet, int col, int row)
    {
        var v = Eval(sheet, col, row);
        return v != null ? v.ToString() : "";
    }

    // Show constant or formula or array formula
    public abstract string Show(int col, int row, Formats fo);

    // Parse string to cell contents at (col, row) in given workbook 
    // Coordinates are zero-based, matching Sheet cell addresses.
    public static Cell Parse(string text, Workbook workbook, int col, int row)
        => Parse(text, workbook, col, row, ReferenceStyle.A1);

    // Parse string to cell contents using the reference notation present in formulas.
    public static Cell Parse(string text, Workbook workbook, int col, int row, ReferenceStyle referenceStyle)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var trimmed = text.TrimStart();
        if (trimmed.StartsWith('='))
        {
            var expression = ExcelFormulaAdapter.Parse(trimmed, workbook, col, row, referenceStyle);
            return expression == null ? null : Formula.Make(workbook, expression);
        }
        if (trimmed.StartsWith('\''))
            return new QuoteCell(trimmed[1..]);
        if (trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"')
            return new TextCell(trimmed[1..^1].Replace("\"\"", "\""));
        if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out double number)
            && !double.IsNaN(number) && !double.IsInfinity(number))
            return new NumberCell(number);
        if (DateTime.TryParse(trimmed, CultureInfo.CurrentCulture, DateTimeStyles.None, out DateTime date))
            return new NumberCell(NumberValue.DoubleFromDateTimeTicks(date.Ticks));
        return null;
    }

    // Add the support range to the cell, avoiding direct self-support at sheet[col,row]
    public void AddSupport(Sheet sheet,
                           int col,
                           int row,
                           Sheet suppSheet,
                           Interval suppCols,
                           Interval suppRows)
    {
        _set ??= new SupportSet();
        _set.AddSupport(sheet, col, row, suppSheet, suppCols, suppRows);
    }

    // Remove sheet[col,row] from the support sets of cells that this cell refers to
    public abstract void RemoveFromSupportSets(Sheet sheet, int col, int row);

    // Remove sheet[col,row] from this cell's support set
    public void RemoveSupportFor(Sheet sheet, int col, int row)
    {
        _set?.RemoveCell(sheet, col, row);
    }

    // Overridden in ArrayFormula?
    public virtual void ForEachSupported(Action<Sheet, int, int> act)
    {
        _set?.ForEachSupported(act);
    }

    // Use at manual cell update, and only if the oldCell is never used again
    public void TransferSupportTo(ref Cell newCell)
    {
        if (_set == null)
            return;

        newCell ??= new BlankCell();
        newCell._set = _set;
    }

    // Add to support sets of all cells referred to from this cell, when
    // the cell appears in the block supported[col..col+cols-1, row..row+rows-1] 
    public abstract void AddToSupportSets(Sheet supported, int col, int row, int cols, int rows);

    // Clear the cell's support set; in ArrayFormula also clear the supportSetUpdated flag
    public virtual void ResetSupportSet() => _set = null;

    public abstract void ForEachReferred(Sheet sheet, int col, int row, Action<FullCellAddr> act);

    // Clone cell (supportSet, state fields) but not its sharable contents
    public abstract Cell CloneCell(int col, int row);

    public abstract void DependsOn(FullCellAddr here, Action<FullCellAddr> dependsOn);
}
