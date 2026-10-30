using System;
using Tabula.Cells;
using Tabula.Values;

namespace Tabula.CellAddressing;

/// <summary>
/// A FullCellAddr is an absolute cell address, including a non-null 
/// reference to a Sheet.
/// </summary>
public struct FullCellAddr : IEquatable<FullCellAddr>
{
    public Sheet Sheet { get; init; }
    public CellAddr Address { get; init; }
    public FullCellAddr(Sheet sheet, CellAddr ca)
    {
        Address = ca;
        Sheet = sheet;
    }

    public FullCellAddr(Sheet sheet, int col, int row)
        : this(sheet, new CellAddr(col, row))
    {
    }

    public FullCellAddr(Sheet sheet, string A1Format)
        : this(sheet, new CellAddr(A1Format))
    {
    }

    public bool Equals(FullCellAddr other)
    {
        return Address.Equals(other.Address) && Sheet == other.Sheet;
    }

    public override int GetHashCode()
    {
        return Address.GetHashCode() * 29 + Sheet.GetHashCode();
    }

    public override bool Equals(object o)
    {
        return o is FullCellAddr addr && Equals(addr);
    }

    public static bool operator ==(FullCellAddr fca1, FullCellAddr fca2)
    {
        return fca1.Address == fca2.Address && fca1.Sheet == fca2.Sheet;
    }

    public static bool operator !=(FullCellAddr fca1, FullCellAddr fca2)
    {
        return fca1.Address != fca2.Address || fca1.Sheet != fca2.Sheet;
    }

    public override string ToString()
    {
        return Sheet.Name + "!" + Address;
    }

    public readonly Value Eval()
    {
        var cell = Sheet[Address];
        if (cell != null)
            return cell.Eval(Sheet, Address.Column, Address.Row);
        else
            return null;
    }

    public readonly bool TryGetCell(out Cell currentCell)
    {
        currentCell = Sheet[Address];
        return currentCell != null;
    }
}