using Tabula.Types;
using Tabula.Values;

namespace Tabula.Expressions;

/// <summary>
/// A TextConst is a constant string-valued expression.
/// </summary>
internal class TextConst : Const
{
    private readonly TextValue _value;
    internal string Value => _value.Value;

    public TextConst(string s)
    {
        _value = TextValue.MakeInterned(s);
    }

    public override Value Eval(Sheet sheet, int col, int row)
    {
        return _value;
    }

    public override string Show(int col, int row, int ctxpre, Formats fo)
    {
        return "\"" + _value + "\"";
    }
}
