using System;

namespace Tabula.CellAddressing;

/// <summary>
/// 
/// A CellAddr represents an absolute cell address in a sheet as a pair of zero-based
/// column and row numbers. RARef represents a formula reference using absolute and
/// relative column and row components.
/// </summary>
[Serializable]
public struct CellAddr : IEquatable<CellAddr>
{
    public int Column { get; init; }
    public int Row { get; init; }

    public CellAddr(int col, int row)
    {
        Column = col;
        Row = row;
    }

    public CellAddr(RARef cr, int col, int row)
    {
        Column = cr.CoolumnAbsolute ? cr.ColumnReference : cr.ColumnReference + col;
        Row = cr.RowAbsolute ? cr.RowReference : cr.RowReference + row;
    }

    // Turn an A1-format string into an absolute cell address
    public CellAddr(string a1Ref) : this(new RARef(a1Ref, 0, 0), 0, 0)
    {
    }

    // Return ulCa as upper left and lrCa as lower right of (ca1, ca2)
    public static void NormalizeArea(CellAddr ca1,
                                     CellAddr ca2,
                                     out CellAddr ulCa,
                                     out CellAddr lrCa)
    {
        int minCol = ca1.Column,
            minRow = ca1.Row,
            maxCol = ca2.Column,
            maxRow = ca2.Row;
        if (ca1.Column > ca2.Column)
        {
            minCol = ca2.Column;
            maxCol = ca1.Column;
        }
        if (ca1.Row > ca2.Row)
        {
            minRow = ca2.Row;
            maxRow = ca1.Row;
        }
        ulCa = new CellAddr(minCol, minRow);
        lrCa = new CellAddr(maxCol, maxRow);
    }

    public CellAddr Offset(CellAddr offset)
    {
        return new CellAddr(Column + offset.Column, Row + offset.Row);
    }

    public bool Equals(CellAddr that)
    {
        return Column == that.Column && Row == that.Row;
    }

    public override int GetHashCode()
    {
        return 29 * Column + Row;
    }

    public override bool Equals(object o)
    {
        return o is CellAddr addr && Equals(addr);
    }

    public static bool operator ==(CellAddr ca1, CellAddr ca2)
    {
        return ca1.Column == ca2.Column && ca1.Row == ca2.Row;
    }

    public static bool operator !=(CellAddr ca1, CellAddr ca2)
    {
        return ca1.Column != ca2.Column || ca1.Row != ca2.Row;
    }

    public override string ToString()
    { // A1 format only
        return ColumnName(Column) + (Row + 1);
    }

    public static string ColumnName(int col)
    {
        var name = string.Empty;
        while (col >= 26)
        {
            name = (char)('A' + col % 26) + name;
            col = col / 26 - 1;
        }
        return (char)('A' + col) + name;
    }
}
