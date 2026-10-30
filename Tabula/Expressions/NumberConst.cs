using System;
using Tabula.Types;
using Tabula.Values;

namespace Tabula.Expressions;

/// <summary>
/// A NumberConst is a constant number-valued expression.
/// </summary>
internal class NumberConst : Const
{
    private readonly NumberValue _value;

    public NumberConst(double d)
    {
        if (double.IsNaN(d) || double.IsInfinity(d))
            throw new ArgumentException();
        _value = (NumberValue)NumberValue.Make(d);
    }

    public override Value Eval(Sheet sheet, int col, int row)
    {
        return _value;
    }

    public override string Show(int col, int row, int ctxpre, Formats fo)
    {
        return _value.ToString();
    }
}
