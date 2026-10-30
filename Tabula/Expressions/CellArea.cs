using System;
using Tabula.CellAddressing;
using Tabula.Types;
using Tabula.Values;

namespace Tabula.Expressions;

/// <summary>
/// A CellArea represents a rectangular range using upper-left and lower-right
/// references. The references retain their relative and absolute components when
/// the formula containing the range is copied or moved.
/// </summary>
internal class CellArea : Calculation, IEquatable<CellArea>
{
    // It would be desirable to store the cell area in normalized form, 
    // with ul always to the left and above lr; but this cannot be done 
    // because it may be mixed relative/absolute as in $A1:$A$5, which 
    // when copied from B1 to B10 is no longer normalized.
    private readonly RARef _ul, _lr; // upper left, lower right
    private readonly Sheet _sheet; // non-null if sheet-absolute

    public CellArea(Sheet sheet,
                    bool ulColAbs,
                    int ulColRef,
                    bool ulRowAbs,
                    int ulRowRef,
                    bool lrColAbs,
                    int lrColRef,
                    bool lrRowAbs,
                    int lrRowRef)
        : this(sheet,
               new RARef(ulColAbs, ulColRef, ulRowAbs, ulRowRef),
               new RARef(lrColAbs, lrColRef, lrRowAbs, lrRowRef))
    {
    }

    public CellArea(Sheet sheet, RARef ul, RARef lr)
    {
        _sheet = sheet;
        _ul = ul;
        _lr = lr;
    }

    // Evaluate cell area by returning an array view of it

    public override Value Eval(Sheet sheet, int col, int row)
    {
        return MakeArrayView(sheet, col, row);
    }

    public ArrayView MakeArrayView(FullCellAddr fca)
    {
        return MakeArrayView(fca.Sheet, fca.Address.Column, fca.Address.Row);
    }

    public ArrayView MakeArrayView(Sheet sheet, int col, int row)
    {
        CellAddr ulCa = _ul.Addr(col, row), lrCa = _lr.Addr(col, row);
        var view = ArrayView.Make(ulCa, lrCa, _sheet ?? sheet);
        // Forcing the evaluation of all cells in an array view value.
        // TODO: Doing this repeatedly is costly
        for (int c = 0; c < view.Cols; c++)
        {
            for (int r = 0; r < view.Rows; r++)
            {
                // Value ignore = view[c, r];
            }
        }
        return view;
    }

    public void ApplyToFcas(Sheet sheet, int col, int row, Action<FullCellAddr> act)
    {
        CellAddr ulCa = _ul.Addr(col, row), lrCa = _lr.Addr(col, row);
        ArrayView.Make(ulCa, lrCa, _sheet ?? sheet).Apply(act);
    }

    // Clone and move (when the containing formula is moved, not copied)
    public override Calculation Move(int deltaCol, int deltaRow)
    {
        return new CellArea(_sheet,
                            _ul.Move(deltaCol, deltaRow),
                            _lr.Move(deltaCol, deltaRow));
    }

    // Can copy cell area with sharing iff corners are within sheet
    public override Calculation CopyTo(int col, int row)
    {
        if (_ul.ValidAt(col, row) && _lr.ValidAt(col, row))
            return this;
        else
            return Error.RefError;
    }

    public override Adjusted<Calculation> InsertRowCols(Sheet modSheet,
                                                 bool thisSheet,
                                                 int R,
                                                 int N,
                                                 int r,
                                                 bool doRows)
    {
        if (_sheet == modSheet || _sheet == null && thisSheet)
        {
            Adjusted<RARef> ulNew = _ul.InsertRowCols(R, N, r, doRows),
                            lrNew = _lr.InsertRowCols(R, N, r, doRows);
            int upper = Math.Min(ulNew.Upper, lrNew.Upper);
            return new Adjusted<Calculation>(new CellArea(_sheet, ulNew.E, lrNew.E),
                                      upper,
                                      ulNew.Same && lrNew.Same);
        }
        else
        {
            return new Adjusted<Calculation>(this);
        }
    }

    internal void AddToSupport(Sheet supported, int col, int row, int cols, int rows)
    {
        Sheet referredSheet = _sheet ?? supported;
        Interval referredRows, referredCols;
        Func<int, Interval> supportedRows, supportedCols;
        int ra = _ul.RowReference, rb = _lr.RowReference, r1 = row, r2 = row + rows - 1;
        RefAndSupp(_ul.RowAbsolute, _lr.RowAbsolute, ra, rb, r1, r2, out referredRows, out supportedRows);
        int ca = _ul.ColumnReference, cb = _lr.ColumnReference, c1 = col, c2 = col + cols - 1;
        RefAndSupp(_ul.CoolumnAbsolute, _lr.CoolumnAbsolute, ca, cb, c1, c2, out referredCols, out supportedCols);
        // Outer iteration should be over the shorter interval for efficiency
        if (referredCols.Length < referredRows.Length)
        {
            referredCols.ForEach(c =>
            {
                Interval suppCols = supportedCols(c);
                referredRows.ForEach(r =>
                                     referredSheet.AddSupport(c, r, supported, suppCols, supportedRows(r)));
            });
        }
        else
        {
            referredRows.ForEach(r =>
            {
                Interval suppRows = supportedRows(r);
                referredCols.ForEach(c =>
                                     referredSheet.AddSupport(c, r, supported, supportedCols(c), suppRows));
            });
        }
    }

    // For formulas in rows r1..r2, compute the referenced row interval and map each
    // referenced row back to the formula rows that depend on it. The endpoints ra
    // and rb can be in either order, and may be relative or absolute.
    private static void RefAndSupp(bool ulAbs,
                                   bool lrAbs,
                                   int ra,
                                   int rb,
                                   int r1,
                                   int r2,
                                   out Interval referred,
                                   out Func<int, Interval> supported)
    {
        if (ulAbs)
        {
            if (lrAbs)
            { // case abs-abs
                SortInts(ref ra, ref rb);
                referred = new Interval(ra, rb);
                supported = r => new Interval(r1, r2);
            }
            else
            { // case abs-rel
                referred = new Interval(Math.Min(ra, r1 + rb), Math.Max(ra, r2 + rb));
                supported = r => ra < r
                                     ? new Interval(Math.Max(r1, r - rb), r2)
                                     : ra > r
                                           ? new Interval(r1, Math.Min(r2, r - rb))
                                           : new Interval(r1, r2);
            }
        }
        else
        {
            if (lrAbs)
            { // case rel-abs
                referred = new Interval(Math.Min(r1 + ra, rb), Math.Max(r2 + ra, rb));
                supported = r => rb > r
                                     ? new Interval(r1, Math.Min(r2, r - ra))
                                     : rb < r
                                           ? new Interval(Math.Max(r1, r - ra), r2)
                                           : new Interval(r1, r2);
            }
            else
            { // case rel-rel
                SortInts(ref ra, ref rb);
                referred = new Interval(r1 + ra, r2 + rb);
                supported = r => new Interval(Math.Max(r1, r - rb), Math.Min(r2, r - ra));
            }
        }
    }

    private static void SortInts(ref int a, ref int b)
    {
        if (a > b)
            (b, a) = (a, b);
    }

    internal override void VisitRefs(RefSet refSet, Action<CellRef> refAct, Action<CellArea> areaAct)
    {
        if (!refSet.SeenBefore(this))
            areaAct(this);
    }

    public override void DependsOn(FullCellAddr here, Action<FullCellAddr> dependsOn)
    {
        ApplyToFcas(here.Sheet, here.Address.Column, here.Address.Row, dependsOn);
    }

    public override bool IsVolatile => false;

    public bool Equals(CellArea that)
    {
        return that != null && _ul.Equals(that._ul) && _lr.Equals(that._lr);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(_ul, _lr);
    }

    public override string Show(int col, int row, int ctxpre, Formats fo)
    {
        var s = _ul.Show(col, row, fo) + ":" + _lr.Show(col, row, fo);
        return _sheet == null ? s : _sheet.Name + "!" + s;
    }

}
