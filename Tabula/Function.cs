using System;
using System.Collections.Generic;
using System.Linq;
using Tabula.Expressions;
using Tabula.Values;

namespace Tabula;

/// <summary>
/// Applier is the delegate type used to represent implementations of
/// built-in functions in the interpretive implementation.
/// </summary>
/// <param name="sheet">The sheet containing the cell in which the function is called.</param>
/// <param name="es">The function call's argument expressions.</param>
/// <param name="col">The column containing the cell in which the function is called.</param>
/// <param name="row">The row containing the cell in which the function is called.</param>
/// <returns></returns>
public delegate Value Applier(Sheet sheet, Calculation[] es, int col, int row);

/// <summary>
/// A Function represents a built-in function or operator.
/// </summary>
public class Function
{
    public string Name { get; private set; }
    public Applier Applier { get; private set; }
    public int Fixity { get; private set; }
    private readonly bool _isVolatile;
    private static readonly IDictionary<string, Function> _table;

    private static readonly Random _random = new Random();

    public static double Average(Value[] vs)
    {
        // May consider whether empty cells and texts should just
        // be ignored instead of given ArgTypeError.
        // We're using Kahan's accurate sum algorithm; see Goldberg 1991.
        double S = 0.0, C = 0.0;
        int count = 0;
        foreach (Value outerV in vs)
        {
            outerV.Apply(delegate (Value v)
            {
                double Y = Value.ToDoubleOrNan(v) - C, T = S + Y;
                C = (T - S) - Y;
                S = T;
                count++;
            });
        }
        return S / count;
    }

    public static double Columns(Value v0)
    {
        if (v0 is ErrorValue)
            return (v0 as ErrorValue).ErrorNan;
        if (v0 is ArrayValue v0arr)
            return v0arr.Cols;
        else
            return ErrorValue.ArgTypeError.ErrorNan;
    }

    public static Value ConstArray(Value v0, Value v1, Value v2)
    {
        if (v0 is ErrorValue)
            return v0;
        if (v1 is ErrorValue)
            return v1;
        if (v2 is ErrorValue)
            return v2;
        if (v1 is NumberValue && v2 is NumberValue)
        {
            int rows = (int)(v1 as NumberValue).Value,
                cols = (int)(v2 as NumberValue).Value;
            if (0 <= rows && 0 <= cols)
            {
                var result = new Value[cols, rows];
                for (int c = 0; c < cols; c++)
                    for (int r = 0; r < rows; r++)
                        result[c, r] = v0;
                return new ArrayExplicit(result);
            }
            else
            {
                return ErrorValue.Make("#ERR: Size");
            }
        }
        else
        {
            return ErrorValue.ArgTypeError;
        }
    }

    public static double Equal(Value e1, Value e2)
    {
        if (e1 is ErrorValue)
            return (e1 as ErrorValue).ErrorNan;
        if (e2 is ErrorValue)
            return (e2 as ErrorValue).ErrorNan;
        return e1 == e2 || e1 != null && e1.Equals(e2) ? 1.0 : 0.0;
    }

    public static double ExcelAtan2(double x, double y)
    {
        return Math.Atan2(y, x); // Note swapped arguments
    }

    public static double ExcelCeiling(double d, double signif)
    {
        return signif * Math.Ceiling(d / signif);
    }

    public static Value ExcelConcat(Value v0, Value v1)
    {
        if (v0 is ErrorValue)
            return v0;
        if (v1 is ErrorValue)
            return v1;
        if ((v0 is TextValue || v0 is NumberValue) && (v1 is TextValue || v1 is NumberValue))
        {
            string s0 = v0.ToString(), s1 = v1.ToString();
            // Avoid creating new String or TextValue objects when possible
            if (string.IsNullOrEmpty(s0))
                return v1 as TextValue ?? TextValue.Make(s1);
            else if (string.IsNullOrEmpty(s1))
                return v0 as TextValue ?? TextValue.Make(s0);
            else
                return TextValue.Make(s0 + s1);
        }
        else
        {
            return ErrorValue.ArgTypeError;
        }
    }

    private static string ExcelText(Value value)
    {
        if (value == null)
            return string.Empty;
        if (value is TextValue text)
            return text.Value;
        if (value is NumberValue number)
            return number.Value.ToString("G", System.Globalization.CultureInfo.InvariantCulture);
        return null;
    }

    private static Value LeftOrRight(Value textValue, Value countValue, bool left)
    {
        if (textValue is ErrorValue)
            return textValue;
        if (countValue is ErrorValue)
            return countValue;
        string text = ExcelText(textValue);
        if (text == null)
            return ErrorValue.ValueError;
        double rawCount = countValue is NumberValue number ? number.Value : countValue == null ? 1 : double.NaN;
        if (double.IsNaN(rawCount))
            return ErrorValue.ValueError;
        if (rawCount < 0)
            return ErrorValue.ValueError;
        int count = rawCount >= int.MaxValue ? int.MaxValue : (int)rawCount;
        count = Math.Min(count, text.Length);
        return TextValue.Make(left ? text.Substring(0, count) : text.Substring(text.Length - count, count));
    }

    private static Value Mid(Value textValue, Value startValue, Value countValue)
    {
        if (textValue is ErrorValue)
            return textValue;
        if (startValue is ErrorValue)
            return startValue;
        if (countValue is ErrorValue)
            return countValue;
        string text = ExcelText(textValue);
        if (text == null || !(startValue is NumberValue start) || !(countValue is NumberValue count))
            return ErrorValue.ValueError;
        if (start.Value < 1 || count.Value < 0)
            return ErrorValue.ValueError;
        int offset = start.Value >= int.MaxValue ? int.MaxValue : (int)start.Value - 1;
        int length = count.Value >= int.MaxValue ? int.MaxValue : (int)count.Value;
        return TextValue.Make(offset >= text.Length ? string.Empty : text.Substring(offset, Math.Min(length, text.Length - offset)));
    }

    private static int CountVector(Value value)
    {
        int count = 0;
        if (value is ArrayValue array)
        {
            array.Apply((Value item) =>
            {
                if (item is NumberValue)
                    count++;
            });
        }
        else if (value is NumberValue)
            count++;
        return count;
    }

    private static int CountAValue(Value value)
    {
        int count = 0;
        if (value is ArrayValue array)
            array.Apply(_ => count++);
        else if (value != null)
            count++;
        return count;
    }

    private static int VectorLength(Value value, out ArrayValue array)
    {
        array = value as ArrayValue;
        if (array == null)
            return value == null ? 0 : 1;
        if (array.Rows == 1)
            return array.Cols;
        if (array.Cols == 1)
            return array.Rows;
        return -1;
    }

    private static Value VectorAt(Value value, ArrayValue array, int index)
    {
        if (array == null)
            return value;
        return array.Rows == 1 ? array[index, 0] : array[0, index];
    }

    private static int CompareLookup(Value left, Value right)
    {
        if (left is NumberValue ln && right is NumberValue rn)
            return ln.Value.CompareTo(rn.Value);
        if (left is TextValue lt && right is TextValue rt)
            return StringComparer.OrdinalIgnoreCase.Compare(lt.Value, rt.Value);
        if (left is NumberValue)
            return -1;
        if (right is NumberValue)
            return 1;
        return StringComparer.OrdinalIgnoreCase.Compare(ExcelText(left) ?? string.Empty, ExcelText(right) ?? string.Empty);
    }

    private static Value Match(Value lookup, Value values, Value modeValue)
    {
        if (lookup is ErrorValue)
            return lookup;
        if (values is ErrorValue)
            return values;
        if (modeValue is ErrorValue)
            return modeValue;
        int mode = modeValue is NumberValue mn ? (int)mn.Value : modeValue == null ? 1 : int.MinValue;
        if (mode != 0 && mode != 1 && mode != -1)
            return ErrorValue.ValueError;
        int length = VectorLength(values, out ArrayValue array);
        if (length < 0)
            return ErrorValue.NaError;
        int best = -1;
        for (int i = 0; i < length; i++)
        {
            Value candidate = VectorAt(values, array, i);
            if (candidate is ErrorValue)
                continue;
            int comparison = CompareLookup(candidate, lookup);
            if (comparison == 0)
            {
                if (mode == 0)
                    return NumberValue.Make(i + 1);
                best = i;
            }
            else if (mode == 1 && comparison < 0)
                best = i;
            else if (mode == -1 && comparison > 0 && (best < 0 || CompareLookup(candidate, VectorAt(values, array, best)) < 0))
                best = i;
        }
        return best < 0 ? ErrorValue.NaError : NumberValue.Make(best + 1);
    }

    private static Value VLookup(Value lookup, Value tableValue, Value columnValue, Value exactValue)
    {
        if (lookup is ErrorValue)
            return lookup;
        if (tableValue is ErrorValue)
            return tableValue;
        if (columnValue is ErrorValue)
            return columnValue;
        if (exactValue is ErrorValue)
            return exactValue;
        if (!(tableValue is ArrayValue table))
            return ErrorValue.ValueError;
        if (!(columnValue is NumberValue colNumber))
            return ErrorValue.ValueError;
        int resultColumn = (int)colNumber.Value - 1;
        if (resultColumn < 0 || resultColumn >= table.Cols)
            return ErrorValue.RefError;
        bool exact = exactValue is NumberValue en && en.Value == 0 || exactValue is TextValue et && et.Value.Equals("FALSE", StringComparison.OrdinalIgnoreCase);
        int best = -1;
        for (int row = 0; row < table.Rows; row++)
        {
            Value candidate = table[0, row];
            if (candidate is ErrorValue)
                continue;
            int comparison = CompareLookup(candidate, lookup);
            if (comparison == 0)
            {
                best = row;
                if (exact)
                    break;
            }
            else if (!exact && comparison < 0)
                best = row;
        }
        if (best < 0)
            return ErrorValue.NaError;
        return table[resultColumn, best] ?? NumberValue.Zero;
    }

    private static Value Divide(Value numerator, Value denominator)
    {
        if (numerator is ErrorValue)
            return numerator;
        if (denominator is ErrorValue)
            return denominator;
        double x = Value.ToDoubleOrNan(numerator), y = Value.ToDoubleOrNan(denominator);
        if (double.IsNaN(x))
            return NumberValue.Make(x);
        if (double.IsNaN(y))
            return NumberValue.Make(y);
        if (y == 0)
            return ErrorValue.Div0Error;
        return NumberValue.Make(x / y);
    }

    public static double ExcelFloor(double d, double signif)
    {
        return signif * Math.Floor(d / signif);
    }

    public static double ExcelMod(double x, double y)
    {
        return x - y * Math.Floor(x / y);
    }

    public static double ExcelNow()
    {
        return NumberValue.DoubleFromDateTimeTicks(DateTime.Now.Ticks);
    }

    public static double ExcelPow(double x, double y)
    {
        // Necessary because MS .NET and Mono Math.Pow is wrong
        return double.IsNaN(x) ? x : double.IsNaN(y) ? y : Math.Pow(x, y);
    }

    public static double ExcelRand()
    {
        return _random.NextDouble();
    }

    public static double ExcelRound(double d, double digits)
    {
        if (double.IsNaN(digits))
        {
            return digits;
        }
        int idigits = (int)digits; // Truncation towards zero is correct
        if (idigits >= 0)
        {
            return Math.Round(d, idigits, MidpointRounding.AwayFromZero);
        }
        else
        {
            double scale = Math.Pow(10, -idigits);
            return scale * Math.Round(d / scale, 0);
        }
    }

    public static Value HArray(Value[] vs)
    {
        Value[,] result = new Value[vs.Length, 1];
        for (int c = 0; c < vs.Length; c++)
        {
            if (vs[c] is ErrorValue)
            {
                return vs[c];
            }
            else
            {
                result[c, 0] = vs[c];
            }
        }
        return new ArrayExplicit(result);
    }

    // Make array as horizontal (side-by-side) concatenation of arguments' columns
    public static Value HCat(Value[] vs)
    {
        int rows = 0, cols = 0;
        foreach (Value v in vs)
        {
            if (v is ErrorValue)
            {
                return v;
            }
            else if (v is ArrayValue)
            {
                rows = Math.Max(rows, (v as ArrayValue).Rows);
                cols += (v as ArrayValue).Cols;
            }
            else
            {
                rows = Math.Max(rows, 1);
                cols += 1;
            }
        }
        foreach (Value v in vs)
            if (v is ArrayValue && (v as ArrayValue).Rows != rows)
                return ErrorValue.Make("#ERR: Row counts differ");

        var result = new Value[cols, rows];
        int nextCol = 0;
        foreach (Value v in vs)
        {
            if (v is ArrayValue arr)
            {
                for (int c = 0; c < arr.Cols; c++)
                    for (int r = 0; r < rows; r++)
                        result[nextCol, r] = arr[c, r];
                    nextCol++;
            }
            else
            {
                for (int r = 0; r < rows; r++)
                    result[nextCol, r] = v;
                nextCol++;
            }
        }
        return new ArrayExplicit(result);
    }

    // Return horizontal array with nv+1 columns cv, fv(cv), fv(fv(cv)), ...
    public static Value Index(Value v0, double r, double c)
    {
        if (v0 is ErrorValue)
            return v0;
        if (double.IsNaN(r))
            return NumberValue.Make(r);
        if (double.IsNaN(c))
            return NumberValue.Make(c);

        if (v0 is ArrayValue arr)
            return arr.Index(r, c);
        else
            return ErrorValue.ArgTypeError;
    }

    public static double IsArray(Value v0)
    {
        if (v0 is ErrorValue value)
            return value.ErrorNan;
        if (v0 == null)
            return ErrorValue.ArgTypeError.ErrorNan;
        else
            return v0 is ArrayValue ? 1.0 : 0.0;
    }

    // This is Excel ISERROR; Excel ISERR does not consider #N/A an error
    public static double IsError(Value v0)
    {
        return v0 is ErrorValue ? 1.0 : 0.0;
    }

    // Generalized n-argument map
    public static double Max(Value[] vs)
    {
        double result = double.NegativeInfinity;
        foreach (Value outerV in vs)
        {
            outerV.Apply(delegate (Value v)
            {
                result = Math.Max(result, Value.ToDoubleOrNan(v));
            });
        }
        return result;
    }

    public static double Min(Value[] vs)
    {
        double result = double.PositiveInfinity;
        foreach (Value outerV in vs)
        {
            outerV.Apply(delegate (Value v)
            {
                result = Math.Min(result, Value.ToDoubleOrNan(v));
            });
        }
        return result;
    }

    public static double Rows(Value v0)
    {
        if (v0 is ErrorValue)
            return (v0 as ErrorValue).ErrorNan;
        if (v0 is ArrayValue v0arr)
            return v0arr.Rows;
        else
            return ErrorValue.ArgTypeError.ErrorNan;
    }

    public static double Sign(double x)
    {
        return double.IsNaN(x) ? x : Math.Sign(x);
    }

    public static Value Slice(Value v0, double r1, double c1, double r2, double c2)
    {
        if (v0 is ErrorValue)
        {
            return v0;
        }
        if (double.IsNaN(r1))
        {
            return NumberValue.Make(r1);
        }
        if (double.IsNaN(c1))
        {
            return NumberValue.Make(c1);
        }
        if (double.IsNaN(r2))
        {
            return NumberValue.Make(r2);
        }
        if (double.IsNaN(c2))
        {
            return NumberValue.Make(c2);
        }
        if (v0 is ArrayValue arr)
        {
            return arr.Slice(r1, c1, r2, c2);
        }
        else
        {
            return ErrorValue.ArgTypeError;
        }
    }

    public static double Sum(Value[] vs)
    {
        // May consider whether empty cells and texts should just
        // be ignored instead of giving ArgTypeError.
        // We're using Kahan's accurate sum algorithm; see Goldberg 1991.
        double S = 0.0, C = 0.0;
        foreach (Value outerV in vs)
        {
            outerV.Apply(delegate (Value v)
            {
                double Y = Value.ToDoubleOrNan(v) - C, T = S + Y;
                C = (T - S) - Y;
                S = T;
            });
        }
        return S;
    }

    public static double SumNew(Value[] vs)
    {
        // Lower-functionality slightly-higher performance SUM.
        // May consider whether empty cells and texts should just
        // be ignored instead of giving ArgTypeError.
        // We're using Kahan's accurate sum algorithm; see Goldberg 1991.
        double S = 0.0, C = 0.0;
        foreach (Value outerV in vs)
        {
            if (outerV is ArrayValue)
            {
                ArrayValue arr = outerV as ArrayValue;
                int cols = arr.Cols, rows = arr.Rows;
                for (int c = 0; c < cols; c++)
                {
                    for (int r = 0; r < rows; r++)
                    {
                        Value v = arr[c, r];
                        if (v != null)
                        { // Only non-blank cells contribute
                            double Y = Value.ToDoubleOrNan(v) - C, T = S + Y;
                            C = (T - S) - Y;
                            S = T;
                        }
                    }
                }
            }
            else if (outerV != null)
            {
                double Y = Value.ToDoubleOrNan(outerV) - C, T = S + Y;
                C = (T - S) - Y;
                S = T;
            }
        }
        return S;
    }

    public static Value Transpose(Value v0)
    {
        if (v0 is ErrorValue)
            return v0;
        if (v0 is ArrayValue v0arr)
        {
            int cols = v0arr.Rows, rows = v0arr.Cols;
            Value[,] result = new Value[cols, rows];
            for (int c = 0; c < cols; c++)
            {
                for (int r = 0; r < rows; r++)
                {
                    result[c, r] = v0arr[r, c];
                }
            }
            return new ArrayExplicit(result);
        }
        else
        {
            return ErrorValue.ArgTypeError;
        }
    }

    public static Value VArray(Value[] vs)
    {
        var result = new Value[1, vs.Length];
        for (int r = 0; r < vs.Length; r++)
        {
            if (vs[r] is ErrorValue)
                return vs[r];
            else
                result[0, r] = vs[r];
        }
        return new ArrayExplicit(result);
    }

    // Make array as vertical concatenation (stack) of the arguments' rows
    public static Value VCat(Value[] vs)
    {
        int rows = 0, cols = 0;
        foreach (Value v in vs)
        {
            if (v is ErrorValue)
                return v;
            else if (v is ArrayValue)
            {
                cols = Math.Max(cols, (v as ArrayValue).Cols);
                rows += (v as ArrayValue).Rows;
            }
            else
            {
                cols = Math.Max(cols, 1);
                rows += 1;
            }
        }
        if (vs.Any(v => v is ArrayValue && ((ArrayValue)v).Cols != cols))
        {
            return ErrorValue.Make("#ERR: Column counts differ");
        }
        Value[,] result = new Value[cols, rows];
        int nextRow = 0;
        foreach (Value v in vs)
        {
            if (v is ArrayValue arr)
            {
                for (int r = 0; r < arr.Rows; r++)
                {
                    for (int c = 0; c < cols; c++)
                        result[c, nextRow] = arr[c, r];
                    nextRow++;
                }
            }
            else
            {
                for (int c = 0; c < cols; c++)
                    result[c, nextRow] = v;
                nextRow++;
            }
        }
        return new ArrayExplicit(result);
    }

    // Return vertical array with nv+1 rows rv, fv(rv), fv(fv(rv)), ...
    public bool IsVolatile => _isVolatile;

    // Get a Function by name
    public static Function Get(string name)
    {
        if (_table.TryGetValue(name.ToUpper(), out Function result))
            return result;
        else
            return null;
    }

    // Populate the table of built-in functions.
    static Function()
    {
        _table = new Dictionary<string, Function>();
        // <fun> : unit -> number
        new Function("NOW",
                     MakeNumberFunction(ExcelNow),
                     isVolatile: true);
        new Function("PI", MakeConstant(NumberValue.Pi));
        new Function("RAND",
                     MakeNumberFunction(ExcelRand),
                     isVolatile: true);
        // <fun> : unit -> #NA error
        new Function("NA", MakeConstant(ErrorValue.NaError));
        // <fun> : number -> number
        new Function("ABS", MakeNumberFunction(Math.Abs));
        new Function("ASIN", MakeNumberFunction(Math.Asin));
        new Function("ACOS", MakeNumberFunction(Math.Acos));
        new Function("ATAN", MakeNumberFunction(Math.Atan));
        new Function("COS", MakeNumberFunction(Math.Cos));
        new Function("EXP", MakeNumberFunction(Math.Exp));
        new Function("LN", MakeNumberFunction((Func<double, double>)Math.Log));
        new Function("LOG", MakeNumberFunction(Math.Log10));
        new Function("LOG10", MakeNumberFunction(Math.Log10));
        new Function("NEG", 9 /* print neatly */, MakeNumberFunction(x => -x));
        new Function("SIN", MakeNumberFunction(Math.Sin));
        new Function("SQRT", MakeNumberFunction(Math.Sqrt));
        new Function("TAN", MakeNumberFunction(Math.Tan));
        // <fun> : number * number -> number
        new Function("ATAN2", MakeNumberFunction(ExcelAtan2));
        new Function("CEILING", MakeNumberFunction(ExcelCeiling));
        new Function("FLOOR", MakeNumberFunction(ExcelFloor));
        new Function("MOD", MakeNumberFunction(ExcelMod));
        new Function("ROUND", MakeNumberFunction(ExcelRound));
        new Function("^", 8, MakeNumberFunction(ExcelPow));
        new Function("*", 7, MakeNumberFunction((x, y) => x * y));
        new Function("/", 7, MakeFunction(Divide));
        new Function("+", 6, MakeNumberFunction((x, y) => x + y));
        new Function("-", 6, MakeNumberFunction((x, y) => x - y));
        new Function("&", 6, MakeFunction(ExcelConcat));
        new Function("<", 5, MakePredicate((x, y) => x < y));
        new Function("<=", 5, MakePredicate((x, y) => x <= y));
        new Function(">=", 5, MakePredicate((x, y) => x >= y));
        new Function(">", 5, MakePredicate((x, y) => x > y));
        new Function("=", 4, MakePredicate((x, y) => x == y));
        new Function("<>", 4, MakePredicate((x, y) => x != y));
        // AND : number* -> number, 
        new Function("AND",
                     // Variadic, and non-strict in args 2, 3, ...
                     delegate (Sheet sheet, Calculation[] es, int col, int row)
                     {
                         foreach (Calculation expr in es)
                         {
                             Value vi = expr.Eval(sheet, col, row);
                             if (vi is NumberValue ni)
                             {
                                 if (Math.Abs(ni.Value) < .001)
                                 {
                                     return NumberValue.Zero;
                                 }
                             }
                             else if (vi is ErrorValue)
                             {
                                 return vi;
                             }
                             else
                             {
                                 return ErrorValue.ArgTypeError;
                             }
                         }
                         return NumberValue.One;
                     });
        //  AVERAGE : (number | array)*  -> number
        new Function("AVERAGE", MakeNumberFunction(Average));
        //  CONSTARRAY : value * number * number -> array
        new Function("CONSTARRAY", MakeFunction(ConstArray));
        // CHOOSE: number * any* -> any
        new Function("CHOOSE",
                     // Variadic, and non-strict in arg 2...
                     delegate (Sheet sheet, Calculation[] es, int col, int row)
                     {
                         if (es.Length >= 1)
                         {
                             Value v0 = es[0].Eval(sheet, col, row);
                             if (v0 is NumberValue n0)
                             {
                                 int index = (int)n0.Value;
                                 if (1 <= index && index < es.Length)
                                 {
                                     return es[index].Eval(sheet, col, row);
                                 }
                                 else
                                 {
                                     return ErrorValue.ValueError;
                                 }
                             }
                             else if (v0 is ErrorValue)
                             {
                                 return v0;
                             }
                             else
                             {
                                 return ErrorValue.ArgTypeError;
                             }
                         }
                         else
                         {
                             return ErrorValue.ArgCountError;
                         }
                     });
        // COLS : array -> number 
        new Function("COLUMNS", MakeNumberFunction(Columns));
        // EQUAL(e1, e2) returns 1 if values e1 and e2 are non-errors and equal
        new Function("EQUAL", MakeNumberFunction(Equal));
        // ERR("message") produces an ErrorValue with the given constant message
        new Function("ERR",
                     delegate (Sheet sheet, Calculation[] es, int col, int row)
                     {
                         if (es.Length != 1)
                         {
                             return ErrorValue.ArgCountError;
                         }
                         if (es[0] is not TextConst messageConst)
                         {
                             return ErrorValue.ArgTypeError;
                         }
                         return ErrorValue.Make("#ERR: " + messageConst.Value);
                     });
        // HARRAY: value* -> array
        new Function("HARRAY", MakeFunction(HArray));
        // HCAT: value* -> array
        new Function("HCAT", MakeFunction(HCat));
        // IF : number any any -> any, 
        new Function("IF",
                     // Note: non-strict in arg 2 and 3
                     delegate (Sheet sheet, Calculation[] es, int col, int row)
                     {
                         if (es.Length == 3)
                         {
                             Value v0 = es[0].Eval(sheet, col, row);
                             if (v0 is NumberValue n0 && !double.IsInfinity(n0.Value) && !double.IsNaN(n0.Value))
                             {
                                 if (Math.Abs(n0.Value) > .001)
                                 {
                                     return es[1].Eval(sheet, col, row);
                                 }
                                 else
                                 {
                                     return es[2].Eval(sheet, col, row);
                                 }
                             }
                             else if (v0 is ErrorValue)
                             {
                                 return v0;
                             }
                             else
                             {
                                 return ErrorValue.ArgTypeError;
                             }
                         }
                         else
                         {
                             return ErrorValue.ArgCountError;
                         }
                     });
        // IFERROR is non-strict in its fallback value.
        new Function("IFERROR",
                     delegate (Sheet sheet, Calculation[] es, int col, int row)
                     {
                         if (es.Length != 2)
                             return ErrorValue.ArgCountError;
                         Value value = es[0].Eval(sheet, col, row);
                         return value is ErrorValue ? es[1].Eval(sheet, col, row) : value;
                     });
        new Function("LEFT", delegate (Sheet sheet, Calculation[] es, int col, int row)
        {
            if (es.Length < 1 || es.Length > 2)
                return ErrorValue.ArgCountError;
            Value text = es[0].Eval(sheet, col, row);
            Value count = es.Length == 2 ? es[1].Eval(sheet, col, row) : NumberValue.One;
            return LeftOrRight(text, count, true);
        });
        new Function("RIGHT", delegate (Sheet sheet, Calculation[] es, int col, int row)
        {
            if (es.Length < 1 || es.Length > 2)
                return ErrorValue.ArgCountError;
            Value text = es[0].Eval(sheet, col, row);
            Value count = es.Length == 2 ? es[1].Eval(sheet, col, row) : NumberValue.One;
            return LeftOrRight(text, count, false);
        });
        new Function("MID", delegate (Sheet sheet, Calculation[] es, int col, int row)
        {
            if (es.Length != 3)
                return ErrorValue.ArgCountError;
            return Mid(es[0].Eval(sheet, col, row), es[1].Eval(sheet, col, row), es[2].Eval(sheet, col, row));
        });
        new Function("LEN", delegate (Sheet sheet, Calculation[] es, int col, int row)
        {
            if (es.Length != 1)
                return ErrorValue.ArgCountError;
            Value value = es[0].Eval(sheet, col, row);
            if (value is ErrorValue)
                return value;
            string text = ExcelText(value);
            return text == null ? ErrorValue.ValueError : NumberValue.Make(text.Length);
        });
        new Function("MATCH", delegate (Sheet sheet, Calculation[] es, int col, int row)
        {
            if (es.Length < 2 || es.Length > 3)
                return ErrorValue.ArgCountError;
            Value lookup = es[0].Eval(sheet, col, row), values = es[1].Eval(sheet, col, row);
            Value mode = es.Length == 3 ? es[2].Eval(sheet, col, row) : null;
            return Match(lookup, values, mode);
        });
        new Function("VLOOKUP", delegate (Sheet sheet, Calculation[] es, int col, int row)
        {
            if (es.Length < 3 || es.Length > 4)
                return ErrorValue.ArgCountError;
            Value lookup = es[0].Eval(sheet, col, row), table = es[1].Eval(sheet, col, row), index = es[2].Eval(sheet, col, row);
            Value exact = es.Length == 4 ? es[3].Eval(sheet, col, row) : null;
            return VLookup(lookup, table, index, exact);
        });
        new Function("COUNT", delegate (Sheet sheet, Calculation[] es, int col, int row)
        {
            int count = 0;
            foreach (Value value in Eval(es, sheet, col, row))
                count += CountVector(value);
            return NumberValue.Make(count);
        });
        new Function("COUNTA", delegate (Sheet sheet, Calculation[] es, int col, int row)
        {
            int count = 0;
            foreach (Value value in Eval(es, sheet, col, row))
                count += CountAValue(value);
            return NumberValue.Make(count);
        });
        // INDEX: any* * number * number  -> any     // (row, col) indexing, offset base 1
        new Function("INDEX", MakeFunction(Index));
        // ISARRAY : value -> number
        new Function("ISARRAY", MakeNumberFunction(IsArray));
        // ISERROR : value -> number -- NOT ErrorValue-strict
        new Function("ISERROR", MakeNumberFunction(IsError));
        // MAX: value * -> number
        new Function("MAX", MakeNumberFunction(Max));
        // MIN: value * -> number
        new Function("MIN", MakeNumberFunction(Min));
        // NOT : number -> number, 
        new Function("NOT",
                     MakeNumberFunction(n0 => double.IsNaN(n0) ? n0 : Math.Abs(n0) < .001 ? 1.0 : 0.0));
        // OR : number number -> number, 
        new Function("OR",
                     // Variadic, and non-strict in args 2, 3, ...
                     delegate (Sheet sheet, Calculation[] es, int col, int row)
                     {
                         foreach (Calculation expr in es)
                         {
                             Value vi = expr.Eval(sheet, col, row);
                             if (vi is NumberValue ni)
                             {
                                 if (ni.Value != 0)
                                 {
                                     return NumberValue.One;
                                 }
                             }
                             else if (vi is ErrorValue)
                             {
                                 return vi;
                             }
                             else
                             {
                                 return ErrorValue.ArgTypeError;
                             }
                         }
                         return NumberValue.Zero;
                     });
        // ROWS : array -> number 
        new Function("ROWS", MakeNumberFunction(Rows));
        // SIGN : number -> number
        new Function("SIGN", MakeNumberFunction(Sign));
        // SLICE : array * number * number * number * number -> value
        new Function("SLICE", MakeFunction(Slice));
        // SUM : { number, array } * -> number
        new Function("SUM", MakeNumberFunction(SumNew));
        // TRANSPOSE : array -> array
        new Function("TRANSPOSE", MakeFunction(Transpose));
        // VARRAY: value* -> array
        new Function("VARRAY", MakeFunction(VArray));
        // VCAT: value* -> array
        new Function("VCAT", MakeFunction(VCat));
        // VOLATILIZE(e1) is exactly as e1, but is volatile
        new Function("VOLATILIZE", MakeFunction(v => v), isVolatile: true);
    }

    private Function(string name, int fixity, Applier applier)
        : this(name, applier, fixity: fixity)
    {
    }

    internal Function(string name,
                      Applier applier,
                      int fixity = 0,
                      bool isVolatile = false,
                      bool register = true)
    {
        Name = name;
        Applier = applier;
        this.Fixity = fixity;
        this._isVolatile = isVolatile;
        if (register)
            _table[name.ToUpperInvariant()] = this;
    }

    public static Function MakeUnknown(string name)
    {
        return new Function(name.ToUpper(),
                            applier: delegate
                            {
                                return ErrorValue.NameError;
                            },
                            register: false);
    }

    // Number-valued nullary function, eg RAND()
    private static Applier MakeNumberFunction(Func<double> dlg)
    {
        return
            delegate (Sheet sheet, Calculation[] es, int col, int row)
            {
                if (es.Length == 0)
                {
                    return NumberValue.Make(dlg());
                }
                else
                {
                    return ErrorValue.ArgCountError;
                }
            };
    }

    // Number-valued constant nullary function, eg PI()
    private static Applier MakeConstant(Value value)
    {
        return
            delegate (Sheet sheet, Calculation[] es, int col, int row)
            {
                if (es.Length == 0)
                {
                    return value;
                }
                else
                {
                    return ErrorValue.ArgCountError;
                }
            };
    }

    // Number-valued strict unary function, eg SIN(x)
    private static Applier MakeNumberFunction(Func<double, double> dlg)
    {
        return
            delegate (Sheet sheet, Calculation[] es, int col, int row)
            {
                if (es.Length == 1)
                {
                    Value v0 = es[0].Eval(sheet, col, row);
                    return NumberValue.Make(dlg(Value.ToDoubleOrNan(v0)));
                }
                else
                {
                    return ErrorValue.ArgCountError;
                }
            };
    }

    // Number-valued strict unary function, eg ISARRAY, ISERROR
    private static Applier MakeNumberFunction(Func<Value, double> dlg)
    {
        return
            delegate (Sheet sheet, Calculation[] es, int col, int row)
            {
                if (es.Length == 1)
                {
                    Value v0 = es[0].Eval(sheet, col, row);
                    return NumberValue.Make(dlg(v0));
                }
                else
                {
                    return ErrorValue.ArgCountError;
                }
            };
    }

    // Number-valued strict binary function, eg +
    private static Applier MakeNumberFunction(Func<double, double, double> dlg)
    {
        return
            delegate (Sheet sheet, Calculation[] es, int col, int row)
            {
                if (es.Length == 2)
                {
                    Value v0 = es[0].Eval(sheet, col, row),
                          v1 = es[1].Eval(sheet, col, row);
                    return NumberValue.Make(dlg(Value.ToDoubleOrNan(v0), Value.ToDoubleOrNan(v1)));
                }
                else
                {
                    return ErrorValue.ArgCountError;
                }
            };
    }

    // Number-valued strict binary function, eg EQUAL(e1,e2)
    private static Applier MakeNumberFunction(Func<Value, Value, double> dlg)
    {
        return
            delegate (Sheet sheet, Calculation[] es, int col, int row)
            {
                if (es.Length == 2)
                {
                    Value v0 = es[0].Eval(sheet, col, row),
                          v1 = es[1].Eval(sheet, col, row);
                    return NumberValue.Make(dlg(v0, v1));
                }
                else
                {
                    return ErrorValue.ArgCountError;
                }
            };
    }

    // Boolean-valued strict binary function, eg ==
    private static Applier MakePredicate(Func<double, double, bool> dlg)
    {
        return
            MakeNumberFunction(delegate (double x, double y)
            {
                return double.IsNaN(x) ? x : double.IsNaN(y) ? y : dlg(x, y) ? 1.0 : 0.0;
            });
    }

    // Number-valued strict variadic function, eg SUM, AVERAGE
    private static Applier MakeNumberFunction(Func<Value[], double> dlg)
    {
        return
            delegate (Sheet sheet, Calculation[] es, int col, int row)
            {
                try
                {
                    return NumberValue.Make(dlg(Eval(es, sheet, col, row)));
                }
                catch (ArgumentException)
                {
                    return ErrorValue.ArgTypeError;
                }
            };
    }

    // Strict unary function, eg TRANSPOSE
    private static Applier MakeFunction(Func<Value, Value> dlg)
    {
        return
            delegate (Sheet sheet, Calculation[] es, int col, int row)
            {
                if (es.Length == 1)
                {
                    return dlg(es[0].Eval(sheet, col, row));
                }
                else
                {
                    return ErrorValue.ArgCountError;
                }
            };
    }

    // Strict binary function, eg &
    private static Applier MakeFunction(Func<Value, Value, Value> dlg)
    {
        return
            delegate (Sheet sheet, Calculation[] es, int col, int row)
            {
                if (es.Length == 2)
                {
                    Value v0 = es[0].Eval(sheet, col, row),
                          v1 = es[1].Eval(sheet, col, row);
                    return dlg(v0, v1);
                }
                else
                {
                    return ErrorValue.ArgCountError;
                }
            };
    }

    // Strict ternary function, eg INDEX
    private static Applier MakeFunction(Func<Value, Value, Value, Value> dlg)
    {
        return
            delegate (Sheet sheet, Calculation[] es, int col, int row)
            {
                if (es.Length == 3)
                {
                    Value v0 = es[0].Eval(sheet, col, row),
                          v1 = es[1].Eval(sheet, col, row),
                          v2 = es[2].Eval(sheet, col, row);
                    return dlg(v0, v1, v2);
                }
                else
                {
                    return ErrorValue.ArgCountError;
                }
            };
    }

    // Strict ternary function, eg INDEX
    private static Applier MakeFunction(Func<Value, double, double, Value> dlg)
    {
        return
            delegate (Sheet sheet, Calculation[] es, int col, int row)
            {
                if (es.Length == 3)
                {
                    Value v0 = es[0].Eval(sheet, col, row),
                          v1 = es[1].Eval(sheet, col, row),
                          v2 = es[2].Eval(sheet, col, row);
                    return dlg(v0, Value.ToDoubleOrNan(v1), Value.ToDoubleOrNan(v2));
                }
                else
                {
                    return ErrorValue.ArgCountError;
                }
            };
    }

    // Strict quinternary function, eg SLICE
    private static Applier MakeFunction(Func<Value, double, double, double, double, Value> dlg)
    {
        return
            delegate (Sheet sheet, Calculation[] es, int col, int row)
            {
                if (es.Length == 5)
                {
                    Value v0 = es[0].Eval(sheet, col, row);
                    double n1 = Value.ToDoubleOrNan(es[1].Eval(sheet, col, row)),
                           n2 = Value.ToDoubleOrNan(es[2].Eval(sheet, col, row)),
                           n3 = Value.ToDoubleOrNan(es[3].Eval(sheet, col, row)),
                           n4 = Value.ToDoubleOrNan(es[4].Eval(sheet, col, row));
                    return dlg(v0, n1, n2, n3, n4);
                }
                else
                {
                    return ErrorValue.ArgCountError;
                }
            };
    }

    // Strict variadic function, eg COUNT
    private static Applier MakeFunction(Func<Value[], Value> dlg)
    {
        return
            delegate (Sheet sheet, Calculation[] es, int col, int row)
            {
                Value[] vs = Eval(es, sheet, col, row);
                return dlg(vs);
            };
    }

    // Evaluate expression array
    private static Value[] Eval(Calculation[] es, Sheet sheet, int col, int row)
    {
        Value[] vs = new Value[es.Length];
        for (int i = 0; i < es.Length; i++)
        {
            vs[i] = es[i].Eval(sheet, col, row);
        }
        return vs;
    }
}
