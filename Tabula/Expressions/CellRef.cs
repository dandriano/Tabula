using System;
using Tabula.CellAddressing;
using Tabula.Types;
using Tabula.Values;



namespace Tabula.Expressions;

/// <summary>
/// A CellRef evaluates a relative or absolute cell reference, optionally bound to a
/// specific sheet.
/// </summary>
internal class CellRef : Calculation, IEquatable<CellRef>
{
    private readonly RARef _raref;
    private readonly Sheet _sheet;

    public CellRef(Sheet sheet, RARef raref)
    {
        _sheet = sheet;
        _raref = raref;
    }

    // Evaluate cell ref by evaluating the cell referred to 
    public override Value Eval(Sheet sheet, int col, int row)
    {
        var ca = _raref.Addr(col, row);
        var cell = (_sheet ?? sheet)[ca];
        return cell?.Eval(sheet, ca.Column, ca.Row);
    }

    public FullCellAddr GetAbsoluteAddr(Sheet sheet, int col, int row)
    {
        return new FullCellAddr(_sheet ?? sheet, _raref.Addr(col, row));
    }

    public FullCellAddr GetAbsoluteAddr(FullCellAddr fca)
    {
        return GetAbsoluteAddr(fca.Sheet, fca.Address.Column, fca.Address.Row);
    }

    // Clone and move (when the containing formula is moved, not copied!)
    public override Calculation Move(int deltaCol, int deltaRow)
    {
        return new CellRef(_sheet, _raref.Move(deltaCol, deltaRow));
    }

    // Can be copied with sharing iff reference is within sheet
    public override Calculation CopyTo(int col, int row)
    {
        if (_raref.ValidAt(col, row))
            return this;
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
            var adj = _raref.InsertRowCols(R, N, r, doRows);
            return new Adjusted<Calculation>(new CellRef(_sheet, adj.E), adj.Upper, adj.Same);
        }
        else
        {
            return new Adjusted<Calculation>(this);
        }
    }

    internal void AddToSupport(Sheet supported, int col, int row, int cols, int rows)
    {
        Sheet referredSheet = _sheet ?? supported;
        int ca = _raref.ColumnReference, ra = _raref.RowReference;
        int r1 = row, r2 = row + rows - 1, c1 = col, c2 = col + cols - 1;
        Interval referredCols, referredRows;
        Func<int, Interval> supportedCols, supportedRows;
        RefAndSupp(_raref.CoolumnAbsolute, ca, c1, c2, out referredCols, out supportedCols);
        RefAndSupp(_raref.RowAbsolute, ra, r1, r2, out referredRows, out supportedRows);
        // Outer iteration is made over the shorter interval for efficiency
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
    // referenced row back to the formula row that depends on it, accounting for
    // whether the reference is relative or absolute.
    private static void RefAndSupp(bool abs,
                                   int ra,
                                   int r1,
                                   int r2,
                                   out Interval referred,
                                   out Func<int, Interval> supported)
    {
        if (abs)
        { // case abs          
            referred = new Interval(ra, ra);
            supported = r => new Interval(r1, r2);
        }
        else
        { // case rel
            referred = new Interval(r1 + ra, r2 + ra);
            supported = r => new Interval(r - ra, r - ra);
        }
    }

    internal override void VisitRefs(RefSet refSet, Action<CellRef> refAct, Action<CellArea> areaAct)
    {
        if (!refSet.SeenBefore(this))
            refAct(this);
    }

    public override void DependsOn(FullCellAddr here, Action<FullCellAddr> dependsOn)
    {
        dependsOn(GetAbsoluteAddr(here));
    }

    public override bool IsVolatile => false;

    public bool Equals(CellRef that)
    {
        return _raref.Equals(that._raref);
    }

    public override int GetHashCode()
    {
        return _raref.GetHashCode();
    }

    public override string Show(int col, int row, int ctxpre, Formats fo)
    {
        var s = _raref.Show(col, row, fo);
        return _sheet == null ? s : _sheet.Name + "!" + s;
    }
}
