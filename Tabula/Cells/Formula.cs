using System;
using System.Collections.Generic;
using Tabula.CellAddressing;
using Tabula.Expressions;
using Tabula.Types;
using Tabula.Values;

namespace Tabula.Cells;

/// <summary>
/// A Formula stores a calculation and caches its evaluated value and recalculation
/// state for the cell that contains it.
/// </summary>
internal sealed class Formula : Cell
{
    public Value Result { get; private set; }
    public Calculation Node { get; private set; }
    public Workbook Workbook { get; private set; }
    public FormulaCellState State { get; private set; }
    public bool Visited
    {
        get => State == FormulaCellState.Uptodate;
        set => State = value ? FormulaCellState.Uptodate : FormulaCellState.Dirty;
    }

    public Formula(Workbook workbook, Calculation node)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(node);
        Node = node;
        Workbook = workbook;
        State = FormulaCellState.Uptodate;
    }

    public static Formula Make(Workbook workbook, Calculation node)
    {
        if (node == null)
            return null;
        else
            return new Formula(workbook, node);
    }

    // FIXME: Adequate for moving one cell, but block moves and row/column
    // inserts should avoid the duplication and unsharing of expressions. 
    public override Cell MoveContents(int deltaCol, int deltaRow)
    {
        return new Formula(Workbook, Node.Move(deltaCol, deltaRow));
    }

    // Evaluate cell's expression if necessary and cache its value; 
    // also enqueue supported cells for evaluation if we use support graph
    public override Value Eval(Sheet sheet, int col, int row)
    {
        switch (State)
        {
            case FormulaCellState.Uptodate:
                break;
            case FormulaCellState.Computing:
                var culprit = new FullCellAddr(sheet, col, row);
                var msg = string.Format("### CYCLE in cell {0} formula {1}",
                                        culprit,
                                        Show(col, row, new Formats()));
                throw new CyclicException(msg, culprit);
            case FormulaCellState.Dirty:
            case FormulaCellState.Enqueued:
                State = FormulaCellState.Computing;
                Result = Node.Eval(sheet, col, row);
                State = FormulaCellState.Uptodate;
                if (Workbook.UseSupportSets)
                    ForEachSupported(EnqueueCellForEvaluation);
                break;
        }
        return Result;
    }

    public override void InsertRowCols(Dictionary<Calculation, Adjusted<Calculation>> adjusted,
                                       Sheet modSheet,
                                       bool thisSheet,
                                       int R,
                                       int N,
                                       int r,
                                       bool doRows)
    {
        Adjusted<Calculation> ae;
        if (adjusted.TryGetValue(Node, out Adjusted<Calculation> value) && r < value.Upper)
            ae = value;
        else
        {
            // Compute a new adjusted expression and insert into the cache
            ae = Node.InsertRowCols(modSheet, thisSheet, R, N, r, doRows);

            if (ae.Same)
                ae = new Adjusted<Calculation>(Node, ae.Upper, ae.Same);
            adjusted[Node] = ae;
        }

        if (r >= ae.Upper)
            throw new InvalidOperationException();

        Node = ae.E;
    }

    public override void MarkDirty()
    {
        if (State != FormulaCellState.Dirty)
        {
            State = FormulaCellState.Dirty;
            ForEachSupported(MarkCellDirty);
        }
    }

    public override void EnqueueForEvaluation(Sheet sheet, int col, int row)
    {
        if (State == FormulaCellState.Dirty)
        {
            State = FormulaCellState.Enqueued;
            sheet.Workbook.AddToQueue(sheet, col, row);
        }
    }

    public override void AddToSupportSets(Sheet supported, int col, int row, int cols, int rows)
    {
        Node.AddToSupportSets(supported, col, row, cols, rows);
    }

    public override void RemoveFromSupportSets(Sheet sheet, int col, int row)
    {
        Node.RemoveFromSupportSets(sheet, col, row);
    }

    // Reset recomputation flags, eg. after a circularity has been found
    public override void ResetCellState()
    {
        State = FormulaCellState.Dirty;
    }

    public override void ForEachReferred(Sheet sheet, int col, int row, Action<FullCellAddr> act)
    {
        Node.ForEachReferred(sheet, col, row, act);
    }

    public override Cell CloneCell(int col, int row)
    {
        return new Formula(Workbook, Node.CopyTo(col, row));
    }

    public override bool IsVolatile => Node.IsVolatile;

    public override void DependsOn(FullCellAddr here, Action<FullCellAddr> dependsOn)
    {
        Node.DependsOn(here, dependsOn);
    }

    public override string ShowValue(Sheet sheet, int col, int row)
    {
        // Use cached value, do not call Eval, as there might be a cycle!
        return Result != null ? Result.ToString() : "";
    }

    public override string Show(int col, int row, Formats fo)
    {
        return "=" + Node.Show(col, row, 0, fo);
    }
}
