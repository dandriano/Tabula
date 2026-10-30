using System;
using Tabula.CellAddressing;
using Tabula.Cells;
using Tabula.Values;

namespace Tabula.Types;

/// <summary>
/// A CachedArrayFormula is shared between multiple array cells.
/// It contains a (caching) array-valued formula, the original cell
/// address of that formula, and the upper left and lower right corners
/// of the array.
/// </summary>
internal sealed class CachedArrayFormula
{
    public Formula Formula { get; init; }
    public Sheet Sheet { get; init; }
    public int FormulaCol { get; }
    public int FormulaRow { get; }
    public CellAddr UlCa { get; }
    public CellAddr LrCa { get; }
    private bool _supportAdded, _supportRemoved; // Referred cells' support sets up to date

    // Invariant: Every cell within the display area sheet[ulCa, lrCa] is an 
    // ArrayFormula whose CachedArrayFormula instance is this one.

    public CachedArrayFormula(Formula formula,
                              Sheet sheet,
                              int formulaCol,
                              int formulaRow,
                              CellAddr ulCa,
                              CellAddr lrCa)
    {
        if (formula == null)
        {
            throw new Exception("CachedArrayFormula arguments");
        }
        else
        {
            Formula = formula;
            Sheet = sheet;
            FormulaCol = formulaCol;
            FormulaRow = formulaRow;
            UlCa = ulCa;
            LrCa = lrCa;
            _supportAdded = _supportRemoved = false;
        }
    }

    // Evaluate expression if necessary
    public Value Eval()
    {
        return Formula.Eval(Sheet, FormulaCol, FormulaRow);
    }

    public CachedArrayFormula MoveContents(int deltaCol, int deltaRow)
    {
        // FIXME: Unshares the formula, shouldn't ...
        return new CachedArrayFormula((Formula)Formula.MoveContents(deltaCol, deltaRow),
                                      Sheet,
                                      FormulaCol,
                                      FormulaRow,
                                      UlCa,
                                      LrCa);
    }

    public Value CachedArray => Formula.Result;

    public void ResetSupportSet()
    {
        // Do NOT clear the underlying formula's support set; it will not be recreated
        _supportAdded = false;
    }

    public void UpdateSupport(Sheet supported)
    {
        // Update the support sets of cells referred from an array formula only once
        if (!_supportAdded)
        {
            Formula.AddToSupportSets(supported, FormulaCol, FormulaRow, 1, 1);
            _supportAdded = true;
        }
    }

    public void RemoveFromSupportSets(Sheet sheet, int col, int row)
    {
        // Update the support sets of cells referred from an array formula only once
        if (!_supportRemoved)
        {
            Formula.RemoveFromSupportSets(sheet, col, row);
            _supportRemoved = true;
        }
    }

    public void ForEachReferred(Action<FullCellAddr> act)
    {
            Formula.ForEachReferred(Sheet, FormulaCol, FormulaRow, act);
    }

    public string Show(int col, int row, Formats fo)
    {
        return Formula.Show(col, row, fo);
    }
}
