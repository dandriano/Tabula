using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Tabula.CellAddressing;
using Tabula.Cells;
using Tabula.Types;

namespace Tabula;

/// <summary>
/// A workbook of class Workbook contains zero or more sheets, organized as a list of
/// non-null Sheet references, where no two references refer to the same Sheet object.
/// </summary>
public sealed class Workbook : IEnumerable<Sheet>
{
    private readonly List<Sheet> _sheets = new List<Sheet>();

    // For managing recalculation of the workbook
    private CyclicException _cyclic;

    public bool UseSupportSets { get; private set; }

    private readonly List<FullCellAddr> _edited
        = new List<FullCellAddr>();

    private readonly HashSet<FullCellAddr> _volatile
        = new HashSet<FullCellAddr>();

    private readonly Queue<FullCellAddr> _evaluation
        = new Queue<FullCellAddr>();

    public Workbook()
    {
        UseSupportSets = false;
    }

    public void AddSheet(Sheet sheet)
    {
        _sheets.Add(sheet);
    }

    public void RecordCellChange(int col, int row, Sheet sheet)
    {
        _edited.Add(new FullCellAddr(sheet, col, row));
    }

    public Sheet this[string name]
    {
        get
        {
            var upperName = name.ToUpper();
            return _sheets.FirstOrDefault(sheet => sheet.Name.Equals(upperName, StringComparison.CurrentCultureIgnoreCase));
        }
    }

    public Sheet this[int i] => _sheets[i];

    // Recalculate from recalculation roots only, using their supported sets
    public double Recalculate()
    {
        // Now Cyclic != null or for all formulas f, f.state==Uptodate
        if (_cyclic != null)
            return RecalculateFull();
        
        return TimeRecalculation(delegate
        {
            UseSupportSets = true;
            // Requires for all formulas f, f.state==Uptodate
            // Stage (1): Mark formulas reachable from roots, f.state=Dirty
            SupportArea.IdempotentForeach = true;
            foreach (var fca in _volatile)
                Cell.MarkCellDirty(fca.Sheet, fca.Address.Column, fca.Address.Row);
            foreach (var fca in _edited)
                Cell.MarkCellDirty(fca.Sheet, fca.Address.Column, fca.Address.Row);
            // Stage (2): Evaluate Dirty formulas (and Dirty cells they depend on)
            _evaluation.Clear();
            SupportArea.IdempotentForeach = true;
            foreach (var fca in _edited)
                Cell.EnqueueCellForEvaluation(fca.Sheet, fca.Address.Column, fca.Address.Row);
            foreach (var fca in _volatile)
                Cell.EnqueueCellForEvaluation(fca.Sheet, fca.Address.Column, fca.Address.Row);
            while (_evaluation.Count > 0)
                _evaluation.Dequeue().Eval();
        });
    }

    public void AddToQueue(Sheet sheet, int col, int row)
    {
        _evaluation.Enqueue(new FullCellAddr(sheet, col, row));
    }

    public double RecalculateFull()
    {
        return TimeRecalculation(delegate
        {
            UseSupportSets = false;
            ResetCellState();
            // For all formulas f, f.state==Dirty
            foreach (var sheet in _sheets)
                sheet.RecalculateFull();
        });
    }

    public double RecalculateFullRebuild()
    {
        return TimeRecalculation(delegate
        {
            UseSupportSets = false;
            RebuildSupportGraph(); // Leaves all cells Dirty
            foreach (var sheet in _sheets)
                sheet.RecalculateFull();
        });
    }

    // Timing, and handling of cyclic dependencies
    private double TimeRecalculation(Action act)
    {
        _cyclic = null;
        var s = DateTime.Now;
        try
        {
            act();
        }
        catch (Exception exn)
        {
            ResetCellState(); // Mark all cells Dirty
            if (exn is CyclicException cyclicException)
                _cyclic = cyclicException;
        }
        _edited.Clear();
        return DateTime.Now.Subtract(s).TotalMilliseconds;
    }

    private void ResetCellState()
    {
        foreach (var sheet in _sheets)
            sheet.ResetCellState();
    }

    public void RebuildSupportGraph()
    {
        foreach (var sheet in this)
            foreach (var cell in sheet)
                cell.ResetSupportSet();

        ResetCellState();
        foreach (var sheet in this)
            sheet.AddToSupportSets();
    }

    public void ResetVolatileSet()
    {
        _volatile.Clear();
        foreach (var sheet in this)
            sheet.IncreaseVolatileSet();
    }

    public void IncreaseVolatileSet(Cell cell, Sheet sheet, int col, int row)
    {
        if (cell != null && cell.IsVolatile)
            _volatile.Add(new FullCellAddr(sheet, col, row));
    }

    public void DecreaseVolatileSet(Cell cell, Sheet sheet, int col, int row)
    {
        if (cell is Formula f)
            _volatile.Remove(new FullCellAddr(sheet, col, row));
    }

    public int SheetCount => _sheets.Count;

    IEnumerator<Sheet> IEnumerable<Sheet>.GetEnumerator()
    {
        return ((IEnumerable<Sheet>)_sheets).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return _sheets.GetEnumerator();
    }
}
