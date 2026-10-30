using System;
using Tabula.Types;

namespace Tabula.Values;

/// <summary>
/// An ErrorValue is a value indicating failure of evaluation.
/// </summary>
public class ErrorValue : Value
{
    public string Message { get; }
    public int Index { get; }

    // Caching ErrorValues by message string, and for access by integer index
    private static readonly ValueCache<string, ErrorValue> _errorTable
        = new ValueCache<string, ErrorValue>((index, message) => new ErrorValue(message, index));

    // NumError is first so it gets index zero, because System.Math functions
    // produce NaN with error code zero.
    public static ErrorValue NumError { get; } = Make("#NUM!");
    public static ErrorValue ArgCountError { get; } = Make("#ERR: ArgCount");
    public static ErrorValue ArgTypeError { get; } = Make("#ERR: ArgType");
    public static ErrorValue NameError { get; } = Make("#NAME?");
    public static ErrorValue RefError { get; } = Make("#REF!");
    public static ErrorValue ValueError { get; } = Make("#VALUE!");
    public static ErrorValue NaError { get; } = Make("#N/A");
    public static ErrorValue Div0Error { get; } = Make("#DIV/0!");
    public static ErrorValue TooManyArgsError { get; } = Make("#ERR: Too many arguments");


    private ErrorValue(string message, int errorIndex)
    {
        Message = message;
        Index = errorIndex;
    }

    public static int GetIndex(string message)
    {
        return _errorTable.GetIndex(message);
    }

    public static ErrorValue Make(string message)
    {
        return _errorTable[_errorTable.GetIndex(message)];
    }

    public double ErrorNan
    {
        get
        {
            return MakeNan(Index);
        }
    }

    // These two are also called from compiled code, through reflection:

    public static ErrorValue FromNan(double d)
    {
        return _errorTable[ErrorCode(d)];
    }

    public static ErrorValue FromIndex(int errorIndex)
    {
        return _errorTable[errorIndex];
    }

    public override bool Equals(Value v)
    {
        return v is ErrorValue err && err.Index == Index;
    }

    public override int GetHashCode()
    {
        return Index;
    }

    public override object ToObject()
    {
        return this;
    }

    public override string ToString()
    {
        return Message;
    }

    // From error code index (int) to NaN (double) and back

    public static double MakeNan(int errorIndex)
    {
        long nanbits = BitConverter.DoubleToInt64Bits(double.NaN);
        return BitConverter.Int64BitsToDouble(nanbits | (uint)errorIndex);
    }

    public static int ErrorCode(double d)
    {
        return (int)BitConverter.DoubleToInt64Bits(d);
    }
}
