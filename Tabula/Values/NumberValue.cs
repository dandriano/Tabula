using System;
using System.Diagnostics;

namespace Tabula.Values;

/// <summary>
/// A NumberValue holds a floating-point number resulting from evaluation.
/// </summary>
public class NumberValue : Value
{
    public double Value { get; }

    public static NumberValue Zero { get; } = new NumberValue(0.0);
    public static NumberValue One { get; } = new NumberValue(1.0);
    public static NumberValue Pi { get; } = new NumberValue(Math.PI);

    private NumberValue(double value)
    {
        Debug.Assert(!double.IsInfinity(value) && !double.IsNaN(value));
        Value = value;
    }

    public static Value Make(double d)
    {
        if (double.IsInfinity(d))
        {
            return ErrorValue.NumError;
        }
        else if (double.IsNaN(d))
        {
            return ErrorValue.FromNan(d);
        }
        else if (d == 0)
        {
            return Zero;
        }
        else if (d == 1)
        {
            return One;
        }
        else
        {
            return new NumberValue(d);
        }
    }

    public override bool Equals(Value v)
    {
        return v is NumberValue && (v as NumberValue).Value == Value;
    }

    public override int GetHashCode()
    {
        return Value.GetHashCode();
    }

    public override object ToObject()
    {
        return Value;
    }

    public static object ToDouble(Value v)
    {
        return v is NumberValue nv ? nv.Value : null; // Causes boxing
    }

    public static Value FromDouble(object o)
    {
        if (o is double)
        {
            return Make((double)o);
        }
        else
        {
            return ErrorValue.NumError;
        }
    }

    public static object ToSingle(Value v)
    {
        return v is NumberValue nv ? (float)nv.Value : null; // Causes boxing
    }

    public static Value FromSingle(object o)
    {
        if (o is float)
        {
            return Make((float)o);
        }
        else
        {
            return ErrorValue.NumError;
        }
    }

    public static object ToInt64(Value v)
    {
        return v is NumberValue nv ? (long)nv.Value : null; // Causes boxing
    }

    public static Value FromInt64(object o)
    {
        if (o is long)
        {
            return Make((long)o);
        }
        else
        {
            return ErrorValue.NumError;
        }
    }

    public static object ToInt32(Value v)
    {
        return v is NumberValue nv ? (int)nv.Value : null; // Causes boxing
    }

    public static Value FromInt32(object o)
    {
        if (o is int)
        {
            return Make((int)o);
        }
        else
        {
            return ErrorValue.NumError;
        }
    }

    public static object ToInt16(Value v)
    {
        return v is NumberValue nv ? (short)nv.Value : null; // Causes boxing
    }

    public static Value FromInt16(object o)
    {
        if (o is short)
        {
            return Make((short)o);
        }
        else
        {
            return ErrorValue.NumError;
        }
    }

    public static object ToSByte(Value v)
    {
        return v is NumberValue nv ? (sbyte)nv.Value : null; // Causes boxing
    }

    public static Value FromSByte(object o)
    {
        if (o is sbyte)
        {
            return Make((sbyte)o);
        }
        else
        {
            return ErrorValue.NumError;
        }
    }

    public static object ToUInt64(Value v)
    {
        return v is NumberValue nv ? (ulong)nv.Value : null; // Causes boxing
    }

    public static Value FromUInt64(object o)
    {
        if (o is ulong)
        {
            return Make((ulong)o);
        }
        else
        {
            return ErrorValue.NumError;
        }
    }

    public static object ToUInt32(Value v)
    {
        return v is NumberValue nv ? (uint)nv.Value : null; // Causes boxing
    }

    public static Value FromUInt32(object o)
    {
        if (o is uint)
        {
            return Make((uint)o);
        }
        else
        {
            return ErrorValue.NumError;
        }
    }

    public static object ToUInt16(Value v)
    {
        return v is NumberValue nv ? (ushort)nv.Value : null; // Causes boxing
    }

    public static Value FromUInt16(object o)
    {
        if (o is ushort)
        {
            return Make((ushort)o);
        }
        else
        {
            return ErrorValue.NumError;
        }
    }

    public static object ToByte(Value v)
    {
        return v is NumberValue nv ? (byte)nv.Value : null; // Causes boxing
    }

    public static Value FromByte(object o)
    {
        if (o is byte)
        {
            return Make((byte)o);
        }
        else
        {
            return ErrorValue.NumError;
        }
    }

    public static object ToBoolean(Value v)
    {
        return v is NumberValue nv ? nv.Value != 0 : null; // Causes boxing
    }

    public static Value FromBoolean(object o)
    {
        if (o is bool)
        {
            return (bool)o ? One : Zero;
        }
        else
        {
            return ErrorValue.NumError;
        }
    }

    // Conversion between System.DateTime ticks and Excel-style date numbers
    private static readonly long _baseDate = new DateTime(1899, 12, 30).Ticks;
    private static readonly double _daysPerTick = 100E-9 / 60 / 60 / 24;

    public static double DoubleFromDateTimeTicks(long ticks)
    {
        return (ticks - _baseDate) * _daysPerTick;
    }

    public override string ToString()
    {
        return Value.ToString();
    }
}
