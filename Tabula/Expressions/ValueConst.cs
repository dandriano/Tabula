using Tabula.Types;
using Tabula.Values;

namespace Tabula.Expressions;

/// <summary>
/// A ValueConst is an arbitrary constant valued expression, used only
/// for partial evaluation; there is no corresponding formula source syntax.
/// </summary>
internal class ValueConst : Const
{
    private readonly Value _value;

    public ValueConst(Value value)
    {
        this._value = value;
    }

    public override Value Eval(Sheet sheet, int col, int row)
    {
        return _value;
    }

    public override string Show(int col, int row, int ctxpre, Formats fo)
    {
        return "ValueConst[" + _value + "]";
    }
}
