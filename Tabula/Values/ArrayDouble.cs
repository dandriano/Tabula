using Tabula.CellAddressing;


namespace Tabula.Values;

/// <summary>
/// An ArrayMatrix is a rectangular array of non-wrapped doubles, 
/// typically the result of evaluating a linear algebra operation, 
/// or of unboxing all elements of an ArrayValue.
/// </summary>
public class ArrayDouble : ArrayValue
{
    // Note: rows and columns are swapped relative to ArrayExplicit,
    // so indexing should go matrix[row, col] -- as in .NET 2D arrays
    public double[,] Matrix { get; }

    public ArrayDouble(int cols, int rows)
    {
        Matrix = new double[rows, cols];
    }

    public ArrayDouble(double[,] matrix)
    {
        Matrix = matrix;
    }

    public override int Cols
    {
        get
        {
            return Matrix.GetLength(1);
        }
    }

    public override int Rows
    {
        get
        {
            return Matrix.GetLength(0);
        }
    }

    public override Value this[int col, int row]
    {
        get
        {
            return NumberValue.Make(Matrix[row, col]);
        }
    }

    public override Value View(CellAddr ulCa, CellAddr lrCa)
    {
        int cols = Cols, rows = Rows, col0 = ulCa.Column, row0 = ulCa.Row;
        var vals = new Value[cols, rows];
        for (int c = 0; c < cols; c++)
            for (int r = 0; r < rows; r++)
                vals[c, r] = NumberValue.Make(Matrix[row0 + r, col0 + c]);
        return new ArrayExplicit(vals);
    }

    public override Value Slice(CellAddr ulCa, CellAddr lrCa)
    {
        return View(ulCa, lrCa);
    }

    // Fast, but external array modification could undermine semantics 
    public override double[,] ToDoubleArray2DFast()
    {
        return Matrix;
    }

    public static Value Make(Value v)
    {
        if (v is ArrayDouble)
        {
            return v;
        }
        else if (v is ArrayValue)
        {
            var arr = v as ArrayValue;
            int cols = arr.Cols, rows = arr.Rows;
            var result = new ArrayDouble(cols, rows);
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                    result.Matrix[r, c] = ToDoubleOrNan(arr[c, r]);

            return result;
        }
        else
        {
            return ErrorValue.ArgTypeError;
        }
    }

    public override bool Equals(Value v)
    {
        return EqualElements(this, v as ArrayValue);
    }
}
