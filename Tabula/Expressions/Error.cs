using Tabula.Types;
using Tabula.Values;

namespace Tabula.Expressions;

/// <summary>
/// An Error expression represents a static error, e.g. invalid cell reference.
/// </summary>
internal class Error : Const
{
    private readonly string _error;
    private readonly ErrorValue _value;
    public static Error RefError { get; } = new Error(ErrorValue.RefError);

    public Error(string msg) : this(ErrorValue.Make(msg))
    {
    }

    public Error(ErrorValue value)
    {
        _value = value;
        _error = _value.ToString();
    }

    public override Value Eval(Sheet sheet, int col, int row)
    {
        return _value;
    }

    public override string Show(int col, int row, int ctxpre, Formats fo)
    {
        return _error;
    }
}
