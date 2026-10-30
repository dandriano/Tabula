using System.Collections.Generic;

using Tabula.Expressions;


namespace Tabula.Types;

/// <summary>
/// A RefSet is a set of CellRefs and CellAreas already seen by a VisitRefs visitor.
/// </summary>
internal class RefSet
{
    private readonly HashSet<CellRef> _cellRefsSeen = new HashSet<CellRef>();
    private readonly HashSet<CellArea> _cellAreasSeen = new HashSet<CellArea>();

    public void Clear()
    {
        _cellRefsSeen.Clear();
        _cellAreasSeen.Clear();
    }

    public bool SeenBefore(CellRef cellRef)
    {
        return !_cellRefsSeen.Add(cellRef);
    }

    public bool SeenBefore(CellArea cellArea)
    {
        return !_cellAreasSeen.Add(cellArea);
    }
}
