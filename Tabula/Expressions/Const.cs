using System;
using Tabula.CellAddressing;
using Tabula.Types;
using Tabula.Values;

namespace Tabula.Expressions;

/// <summary>
/// 
/// A Const expression is a constant, immutable and sharable.
/// </summary>
internal abstract class Const : Calculation
{
    public static Const Make(Value value)
    {
        if (value is NumberValue number)
            return new NumberConst(number.Value);
        else if (value is TextValue text)
            return new TextConst(text.Value);
        else
            return new ValueConst(value);
    }

    public override Calculation Move(int deltaCol, int deltaRow)
    {
        return this;
    }

    // Any expression can be copied with sharing
    public override Calculation CopyTo(int col, int row)
    {
        return this;
    }

    public override Adjusted<Calculation> InsertRowCols(Sheet modSheet,
                                                 bool thisSheet,
                                                 int R,
                                                 int N,
                                                 int r,
                                                 bool doRows)
    {
        return new Adjusted<Calculation>(this);
    }

    internal override void VisitRefs(RefSet refSet, Action<CellRef> refAct, Action<CellArea> areaAct)
    {
    }

    public override void DependsOn(FullCellAddr here, Action<FullCellAddr> dependsOn)
    {
    }

    public override bool IsVolatile => false;
}
