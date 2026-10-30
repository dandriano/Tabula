using System;
using Tabula.Types;

namespace Tabula.Values;

/// <summary>
/// A TextValue holds a string resulting from evaluation.
/// </summary>
public class TextValue : Value
{
    public string Value { get; }

    // Caching TextValues by string contents, and for access by integer index
    private static ValueCache<string, TextValue> _textValueCache
        = new ValueCache<string, TextValue>((index, s) => new TextValue(s));

    public static TextValue Empty { get; } = MakeInterned(string.Empty);
    public static TextValue Void { get; } = MakeInterned("<void>");

    private TextValue(string value)
    {
        Value = value;
    }

    public static int GetIndex(string s)
    {
        return _textValueCache.GetIndex(s);
    }

    public static TextValue MakeInterned(string s)
    {
        return _textValueCache[_textValueCache.GetIndex(s)];
    }

    public static TextValue Make(string s)
    {
        ArgumentNullException.ThrowIfNull(s);
        if (s.Length == 0)
            return Empty;
        else
            return new TextValue(s); // On purpose NOT interned!
    }

    // These five are called also from generated code:

    public static TextValue FromIndex(int index)
    {
        return _textValueCache[index];
    }

    public static Value FromString(object o)
    {
        if (o is string)
            return Make(o as string);
        else
            return ErrorValue.ArgTypeError;
    }

    public static string ToString(Value v)
    {
        return v is TextValue tv ? tv.Value : null;
    }

    public static Value FromNakedChar(char c)
    {
        return Make(c.ToString());
    }

    public static char ToNakedChar(TextValue v)
    {
        return v.Value.Length >= 1 ? v.Value[0] : '\0';
    }

    public static Value FromChar(object o)
    {
        if (o is char v)
            return Make(v.ToString());
        else
            return ErrorValue.ArgTypeError;
    }

    public static object ToChar(Value v)
    {
        return v is TextValue tv && tv.Value.Length >= 1 ? tv.Value[0] : null; // causes boxing
    }

    public override bool Equals(Value v)
    {
        return v is TextValue && (v as TextValue).Value.Equals(Value);
    }

    public override int GetHashCode()
    {
        return Value.GetHashCode();
    }

    public override object ToObject()
    {
        return Value;
    }

    public override string ToString()
    {
        return Value;
    }
}
