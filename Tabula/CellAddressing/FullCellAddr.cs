using System;
using Tabula.Cells;
using Tabula.Values;

namespace Tabula.CellAddressing;

/// <summary>
/// A FullCellAddr is an absolute cell address, including a non-null 
/// reference to a Sheet.
/// </summary>
public readonly record struct FullCellAddr
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

    public override int GetHashCode()
    {
        return HashCode.Combine(Address, Sheet);
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
