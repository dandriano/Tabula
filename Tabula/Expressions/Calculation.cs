using System;
using Irony.Parsing;
using Tabula.CellAddressing;
using Tabula.Cells;
using Tabula.Types;
using Tabula.Values;

namespace Tabula.Expressions;

/// <summary>
/// A Calculation is an expression-tree node that can be evaluated, moved or copied
/// with its references adjusted, and queried for cell dependencies.
/// </summary>
public abstract class Calculation : ParseTreeNode, IDepend
{
    protected Calculation() : base(new NonTerminal(nameof(Calculation)), default)
    {
    }
    // Update cell references when containing cell is moved (not copied)
    public abstract Calculation Move(int deltaCol, int deltaRow);

    // Invalidate off-sheet references when containing cell is copied (not moved)
    public abstract Calculation CopyTo(int col, int row);

    // Evaluate expression as if at cell address sheet[col, row]
    public abstract Value Eval(Sheet sheet, int col, int row);

    // Insert N new rowcols before rowcol R>=0, when we're at rowcol r
    public abstract Adjusted<Calculation> InsertRowCols(Sheet modSheet,
                                                        bool thisSheet,
                                                        int R,
                                                        int N,
                                                        int r,
                                                        bool doRows);

    // Apply refAct once to each CellRef in expression, and areaAct once to each CellArea
    internal abstract void VisitRefs(RefSet refSet,
                                     Action<CellRef> refAct,
                                     Action<CellArea> areaAct);

    // Increase the support sets of all cells referred from this expression, when
    // the expression appears in the block supported[col..col+cols-1, row..row+rows-1] 
    internal void AddToSupportSets(Sheet supported, int col, int row, int cols, int rows)
    {
        VisitRefs(new RefSet(),
                  cellRef => cellRef.AddToSupport(supported, col, row, cols, rows),
                  cellArea => cellArea.AddToSupport(supported, col, row, cols, rows));
    }

    // Remove sheet[col, row] from the support sets of cells referred from this expression
    public void RemoveFromSupportSets(Sheet sheet, int col, int row)
    {
        ForEachReferred(sheet,
                        col,
                        row,
                        // Remove sheet[col,row] from support set at fca
                        delegate (FullCellAddr fca)
                        {
                            if (fca.TryGetCell(out Cell cell)) // Will be non-null if support correctly added
                                cell.RemoveSupportFor(sheet, col, row);
                        });
    }

    // Apply act, once only, to the full cell address of each cell referred from expression
    public void ForEachReferred(Sheet sheet, int col, int row, Action<FullCellAddr> act)
    {
        VisitRefs(new RefSet(),
                  cellRef => act(cellRef.GetAbsoluteAddr(sheet, col, row)),
                  areaRef => areaRef.ApplyToFcas(sheet, col, row, act));
    }

    // Call dependsOn(fca) on all cells fca referred from expression, with multiplicity.
    // Cannot be implemented in terms of VisitRefs, which visits only once.
    public abstract void DependsOn(FullCellAddr here, Action<FullCellAddr> dependsOn);

    // True if expression textually contains a call to a volatile function
    public abstract bool IsVolatile { get; }

    // Show contents as expression
    public abstract string Show(int col, int row, int ctxpre, Formats fo);
}
