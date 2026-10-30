using System;
using System.Collections;
using System.Collections.Generic;
using Tabula.CellAddressing;
using Tabula.Cells;
using Tabula.Expressions;
using Tabula.Types;

namespace Tabula;

/// <summary>
/// A Sheet stores cells for one named worksheet and tracks its current dimensions
/// and containing workbook.
/// </summary>
public sealed class Sheet : IEnumerable<Cell>
{
    public Workbook Workbook { get; }
    private readonly SheetRep _cells = new SheetRep();
    public string Name { get; set; }
    public int Cols { get; private set; }
    public int Rows { get; private set; }

    public Sheet(Workbook workbook, string name, int cols, int rows)
    {
        Workbook = workbook;
        Name = name;
        Cols = cols;
        Rows = rows;
        workbook.AddSheet(this);
    }

    // Recalculate all cells by evaluating their contents
    public void RecalculateFull()
    {
        _cells.Forall((col, row, cell) => cell.Eval(this, col, row));
    }

    // Show the contents of all non-null cells
    public void ShowAll(Action<int, int, string> show)
    {
        // Should use cells.Forall(show) but that may not clean up newly empty cells?
        for (int col = 0; col < Cols; col++)
        {
            for (int row = 0; row < Rows; row++)
            {
                var cell = _cells[col, row];
                show(col, row, cell != null ? ShowValue(col, row) : null);
            }
        }
    }

    // Reset recomputation flags after a circularity has been found
    public void ResetCellState()
    {
        foreach (var cell in _cells)
            cell?.ResetCellState();
    }

    // From parsed constant or formula or null but not array formula, at sheet[col, row]
    public void SetCell(Cell cell, int col, int row)
    {
        if(cell is ArrayFormula)
            throw new InvalidOperationException();
        this[col, row] = cell;
        cell?.AddToSupportSets(this, col, row, 1, 1);
    }

    // Insert cell, which must be Formula, as array formula 
    // in area ((ulCol,ulRow), (lrCol, lrRow))
    public void SetArrayFormula(Cell cell, int col, int row, CellAddr ulCa, CellAddr lrCa)
    {
        var formula = cell as Formula;
        if (cell == null)
            throw new InvalidOperationException("Invalid array formula");

        var caf = new CachedArrayFormula(formula, this, col, row, ulCa, lrCa);
        // Increase support sets of cells referred by formula
        formula.AddToSupportSets(this, col, row, 1, 1);
        Interval displayCols = new Interval(ulCa.Column, lrCa.Column),
                    displayRows = new Interval(ulCa.Row, lrCa.Row);
        // The underlying formula supports (only) the ArrayFormula cells in display range
        formula.ResetSupportSet();
        formula.AddSupport(this, col, row, this, displayCols, displayRows);
        int cols = lrCa.Column - ulCa.Column + 1, rows = lrCa.Row - ulCa.Row + 1;
        for (int c = 0; c < cols; c++)
            for (int r = 0; r < rows; r++)
                this[ulCa.Column + c, ulCa.Row + r] = new ArrayFormula(caf, c, r);
    }

    // Copy cell to area ((col,row), (col+cols-1,row+rows-1))
    // Probably makes no sense when the cell is an array formula
    public void PasteCell(Cell cell, int col, int row, int cols, int rows)
    {
        // TODO: When cell contains CellRef or CellArea, should check that
        // references are not invalid, as in A0 or A-1:B2
        for (int c = 0; c < cols; c++)
            for (int r = 0; r < rows; r++)
                this[col + c, row + r] = cell.CloneCell(col, row);
        cell.AddToSupportSets(this, col, row, cols, rows);
    }

    // Move cell (fromCol, fromRow) to cell (col, row)
    public void MoveCell(int fromCol, int fromRow, int col, int row)
    {
        var cell = _cells[fromCol, fromRow];
        this[col, row] = cell.MoveContents(col - fromCol, row - fromRow);
    }

    // Insert N new rows just before row R >= 0
    // TODO: Consider replacing all assignments to cells[,] with assignments to this[,],
    // for proper maintenance of support sets and volatile status
    public void InsertRowCols(int R, int N, bool doRows)
    {
        // Check that this will not split a array formula
        if (R >= 1)
        {
            if (doRows)
            {
                for (int col = 0; col < Cols; col++)
                {
                    var cell = _cells[col, R - 1];
                    if (cell is ArrayFormula mf && mf.Contains(col, R))
                        throw new InvalidOperationException("Row insert would split array formula");
                }
            }
            else
            {
                for (int row = 0; row < Rows; row++)
                {
                    var cell = _cells[R - 1, row];
                    if (cell is ArrayFormula mf && mf.Contains(R, row))
                        throw new InvalidOperationException("Column insert would split array formula");
                }
            }
        }
        // Adjust formulas in all sheets.  The dictionary records adjusted
        // expressions to preserve sharing of expressions where possible.
        var adjusted = new Dictionary<Calculation, Adjusted<Calculation>>();
        foreach (var sheet in Workbook)
        {
            for (int r = 0; r < sheet.Rows; r++)
            {
                for (int c = 0; c < sheet.Cols; c++)
                {
                    Cell cell = sheet._cells[c, r];
                    cell?.InsertRowCols(adjusted,
                                        this,
                                        sheet == this,
                                        R,
                                        N,
                                        doRows ? r : c,
                                        doRows);
                }
            }
        }
        if (doRows)
        {
            // Move the rows R, R+1, ... later by N rows in current sheet
            for (int r = Rows - 1; r >= R + N; r--)
                for (int c = 0; c < Cols; c++)
                    _cells[c, r] = _cells[c, r - N];
            // Finally, null out the fresh rows
            for (int r = 0; r < N; r++)
                for (int c = 0; c < Cols; c++)
                    _cells[c, r + R] = null;
        }
        else
        {
            // Move the columns R, R+1, ... later by N columns in current sheet
            for (int c = Cols - 1; c >= R + N; c--)
                for (int r = 0; r < Rows; r++)
                    _cells[c, r] = _cells[c - N, r];
            // Finally, null out the fresh columns
            for (int c = 0; c < N; c++)
                for (int r = 0; r < Rows; r++)
                    _cells[c + R, r] = null;
        }
    }

    // Show contents, if any, of cell at (col, row)
    public string Show(int col, int row)
    {
        if (0 <= col && col < Cols && 0 <= row && row < Rows)
        {
            var cell = _cells[col, row];
            return cell?.Show(col, row, new Formats());
        }
        return null;
    }

    // Show value (or contents), if any, of cell (col, row)
    public string ShowValue(int col, int row)
    {
        if (0 <= col && col < Cols && 0 <= row && row < Rows)
        {
            var cell = _cells[col, row];
            if (cell == null)
                return null;

                if (cell is Formula)
                    return cell.Show(col, row, new Formats());

                return cell.ShowValue(this, col, row);
        }
        return null;
    }

    // Get and set cell contents, maintaining support and volatility information
    public Cell this[int col, int row]
    {
        get
        {
            return col < Cols && row < Rows ? _cells[col, row] : null;
        }
        set
        {
            if (col < Cols && row < Rows)
            {
                var oldCell = _cells[col, row];
                if (oldCell != value)
                {
                    if (oldCell is Formula or ArrayFormula || value is Formula or ArrayFormula)
                        Workbook.InvalidateSupportMetadata();

                    if (oldCell != null)
                    {
                        oldCell.TransferSupportTo(ref value); // Assumes oldCell used nowhere else
                    Workbook.DecreaseVolatileSet(oldCell, this, col, row);
                        oldCell.RemoveFromSupportSets(this, col, row);
                    }
                    Workbook.IncreaseVolatileSet(value, this, col, row);
                    // We do not add to support sets here; that would fragment the support sets
                    _cells[col, row] = value;
                    Workbook.RecordCellChange(col, row, this);
                }
            }
        }
    }

    public Cell this[CellAddr ca]
    {
        get => this[ca.Column, ca.Row];
        private set => this[ca.Column, ca.Row] = value;
    }

    public IEnumerator<Cell> GetEnumerator()
    {
        return _cells.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    // Detect blocks of formula copies, for finding compact support sets
    public void AddToSupportSets()
    {
        int sheetCols = Cols, sheetRows = Rows;
        _cells.Forall((col, row, cell) =>
        {
            if (cell is ArrayFormula)
            {
                // Do not try to detect copies of array formulas.
                // CHECK THIS!
                ArrayFormula af = cell as ArrayFormula;
                af.AddToSupportSets(this, col, row, 1, 1);
            }
            else if (cell is Formula formula && !formula.Visited)
            {
                var expr = formula.Node;
                // (1) Find a large rectangle containing only formula f
                // (1a) Find largest square of copies with upper left corner = (col, row)
                var size = 1;
                while (col + size < sheetCols && row + size < sheetRows
                        && CheckCol(col + size, row, expr, size)
                        && CheckRow(col, row + size, expr, size))
                    size++;
                // All cells in sheet[col..col+size-1, row..row+size-1] contain expression expr
                // (1b) Try extending square with more rows below
                var rowSize = size;
                while (row + rowSize < sheetRows && CheckRow(col, row + rowSize, expr, size - 1))
                    rowSize++;
                // sheet[col..col+size-1, row..row+rows-1] contains expr
                // (1c) Try extending square with more columns to the right
                var cols = size;
                while (col + cols < sheetCols && CheckCol(col + cols, row, expr, size - 1))
                    cols++;
                // sheet[col..col+cols-1, row..row+size-1] contains expr
                if (rowSize > cols)
                    cols = size;
                else
                    rowSize = size;
                // All cells in sheet[col..col+cols-1, row..row+rows-1] contain expression expr
                // (2) Mark all cells in the rectangle visited
                for (int deltaCol = 0; deltaCol < cols; deltaCol++)
                    for (int deltaRow = 0; deltaRow < rowSize; deltaRow++)
                        (this[col + deltaCol, row + deltaRow] as Formula).Visited = true;
                // (3) Update the support sets of cells referred to from expr
                expr.AddToSupportSets(this, col, row, cols, rowSize);
            }
        });
        ResetCellState(); // Undo changes made to the sheet's cells' states
    }

    // Check the row sheet[col..col+size, row] for formulas identical to expr
    private bool CheckRow(int col, int row, Calculation expr, int size)
    {
        for (int deltaCol = 0; deltaCol <= size; deltaCol++)
            if (this[col + deltaCol, row] is not Formula fcr || fcr.Visited || fcr.Node != expr)
                return false;
        return true;
    }

    // Check the column sheet[col, row..row+size] for formulas identical to expr
    private bool CheckCol(int col, int row, Calculation expr, int size)
    {
        for (int deltaRow = 0; deltaRow <= size; deltaRow++)
            if (this[col, row + deltaRow] is not Formula fcr || fcr.Visited || fcr.Node != expr)
                return false;
        return true;
    }

    // Add supportedSheet[supportedColumns, supportedRows] to support set of this[col,row]
    public void AddSupport(int col,
                           int row,
                           Sheet supportedSheet,
                           Interval supportedCols,
                           Interval supportedRows)
    {
        if (this[col, row] == null)
            this[col, row] = new BlankCell();
        this[col, row]?.AddSupport(this, col, row, supportedSheet, supportedCols, supportedRows);
    }

    public void IncreaseVolatileSet()
    {
        _cells.Forall((col, row, cell) => Workbook.IncreaseVolatileSet(cell, this, col, row));
    }
}
