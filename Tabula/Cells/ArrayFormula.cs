using System;
using System.Collections.Generic;
using Tabula.CellAddressing;
using Tabula.Expressions;
using Tabula.Types;
using Tabula.Values;

namespace Tabula.Cells;

/// <summary>
/// An ArrayFormula cell reads one component from the cached result shared by all
/// cells displaying the same array formula.
/// </summary>
internal sealed class ArrayFormula : Cell
{
    private readonly CachedArrayFormula _caf;
    private readonly CellAddr _ca;

    public ArrayFormula(CachedArrayFormula caf, CellAddr ca)
    {
        ArgumentNullException.ThrowIfNull(caf);
        ArgumentNullException.ThrowIfNull(ca);
        _caf = caf;
        _ca = ca;
    }

    public ArrayFormula(CachedArrayFormula caf, int col, int row)
        : this(caf, new CellAddr(col, row))
    {
    }

    public override Value Eval(Sheet sheet, int col, int row)
    {
        var v = _caf.Eval();
        if (v is ArrayValue)
            return (v as ArrayValue)[_ca];
        else if (v is ErrorValue)
            return v;
        else
            return ErrorValue.Make("#ERR: Not array");
    }

    public bool Contains(int col, int row)
    {
        return _caf.UlCa.Column <= col && col <= _caf.LrCa.Column
               && _caf.UlCa.Row <= row && row <= _caf.LrCa.Row;
    }

    public override Cell MoveContents(int deltaCol, int deltaRow)
    {
        // FIXME: loses sharing of the CachedArrayFormula; but then again
        // an array formula should never be moved cell by cell, but in its
        // entirety, and in one go.
        return new ArrayFormula(_caf.MoveContents(deltaCol, deltaRow), _ca);
    }

    public override void InsertRowCols(Dictionary<Calculation, Adjusted<Calculation>> adjusted,
                                       Sheet modSheet,
                                       bool thisSheet,
                                       int R,
                                       int N,
                                       int r,
                                       bool doRows)
    {
        // FIXME: Implement, make sure to update underlying formula only once
        throw new NotImplementedException("Insertions that move array formulas");
    }

    public override string ShowValue(Sheet sheet, int col, int row)
    {
        // Use the underlying cached value, do not call Eval, there might be a cycle!
        var v = _caf.CachedArray;
        if (v is ArrayValue)
        {
            var element = (v as ArrayValue)[_ca];
            return element != null ? element.ToString() : "";
        }
        else if (v is ErrorValue)
        {
            return v.ToString();
        }
        else
        {
            return ErrorValue.Make("#ERR: Not array").ToString();
        }
    }

    public override void MarkDirty()
    {
        switch (_caf.Formula.State)
        {
            case FormulaCellState.Uptodate:
                _caf.Formula.MarkDirty();
                ForEachSupported(MarkCellDirty);
                // caf.formula will call MarkDirty on the array formula's display 
                // cells, except this one, so must mark its dependents explicitly. 
                break;
            case FormulaCellState.Dirty:
                ForEachSupported(MarkCellDirty);
                break;
        }
    }

    public override void EnqueueForEvaluation(Sheet sheet, int col, int row)
    {
        switch (_caf.Formula.State)
        {
            case FormulaCellState.Dirty:
                _caf.Eval();
                ForEachSupported(EnqueueCellForEvaluation);
                // caf.formula will call EnqueueForEvaluation on the array formula's
                // display cells, except this one, so must enqueue its dependents.
                break;
            case FormulaCellState.Uptodate:
                ForEachSupported(EnqueueCellForEvaluation);
                break;
        }
    }

    public override void ResetCellState()
    {
        _caf.Formula.ResetCellState();
    }

    public override void ResetSupportSet()
    {
        _caf.ResetSupportSet();
        base.ResetSupportSet();
    }

    public override void AddToSupportSets(Sheet supported, int col, int row, int cols, int rows)
    {
        _caf.UpdateSupport(supported);
    }

    public override void RemoveFromSupportSets(Sheet sheet, int col, int row)
    {
        _caf.RemoveFromSupportSets(sheet, col, row);
    }

    public override void ForEachReferred(Sheet sheet, int col, int row, Action<FullCellAddr> act)
    {
        _caf.ForEachReferred(act);
    }

    public override Cell CloneCell(int col, int row)
    {
        // Not clear how to copy an array formula.  It does make sense; see Excel.
        throw new System.NotImplementedException();
    }

    public override bool IsVolatile => _caf.Formula.IsVolatile;

    public override void DependsOn(FullCellAddr here, Action<FullCellAddr> dependsOn)
    {
        // It seems that this could uselessly be called on every cell 
        // that shares the formula, but this will not happen on function sheets
        _caf.Formula.DependsOn(here, dependsOn);
    }

    public override string Show(int col, int row, Formats fo)
    {
        return "{" + _caf.Show(_caf.FormulaCol, _caf.FormulaRow, fo) + "}";
    }
}
