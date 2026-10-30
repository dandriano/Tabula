using System;
using Tabula.Types;

namespace Tabula.CellAddressing;

/// <summary>
/// 
/// An RARef stores a formula reference as absolute or relative column and row
/// components. Relative components are offsets from the cell containing the formula;
/// absolute components are zero-based coordinates.
/// </summary>
public sealed class RARef : IEquatable<RARef>
{
    public bool CoolumnAbsolute { get; init; }
    public bool RowAbsolute { get; init; }
    public int ColumnReference { get; init; }
    public int RowReference { get; init; }

    public RARef(bool colAbs, int colRef, bool rowAbs, int rowRef)
    {
        CoolumnAbsolute = colAbs;
        ColumnReference = colRef;
        RowAbsolute = rowAbs;
        RowReference = rowRef;
    }

    // Parse "$A$3" to (true,0,true,2) and so on; relative references
    // must be adjusted to the (col,row) in which the reference occurs.

    public RARef(string a1Ref, int col, int row)
    {
        /* RC denotes "this" cell in MS XMLSS format; needed for area support */
        if (a1Ref.Equals("rc", StringComparison.CurrentCultureIgnoreCase))
        {
            CoolumnAbsolute = false;
            RowAbsolute = false;
            ColumnReference = 0;
            RowReference = 0;
        }
        else
        {
            int i = 0;
            if (i < a1Ref.Length && a1Ref[i] == '$')
            {
                CoolumnAbsolute = true;
                i++;
            }
            int val = -1;
            while (i < a1Ref.Length && IsAToZ(a1Ref[i]))
            {
                val = (val + 1) * 26 + AToZValue(a1Ref[i]);
                i++;
            }
            ColumnReference = CoolumnAbsolute ? val : val - col;
            if (i < a1Ref.Length && a1Ref[i] == '$')
            {
                RowAbsolute = true;
                i++;
            }
            val = ParseInt(a1Ref, ref i);
            RowReference = (RowAbsolute ? val : val - row) - 1;
        }
    }

    // Parse XMLSS R1C1 notation from well-formed string (checked by parser)

    public RARef(string r1c1)
    {
        int i = 0;
        RowAbsolute = true;
        CoolumnAbsolute = true;
        if (i < r1c1.Length && r1c1[i] == 'R')
            i++;
        if (i < r1c1.Length && r1c1[i] == '[')
        {
            RowAbsolute = false;
            i++;
        }
        int val = ParseInt(r1c1, ref i);
        if (RowAbsolute && val == 0)
            RowAbsolute = false;
        RowReference = RowAbsolute ? val - 1 : val;
        if (i < r1c1.Length && r1c1[i] == ']')
            i++;
        if (i < r1c1.Length && r1c1[i] == 'C')
            i++;
        if (i < r1c1.Length && r1c1[i] == '[')
        {
            CoolumnAbsolute = false;
            i++;
        }
        val = ParseInt(r1c1, ref i);
        if (i < r1c1.Length && r1c1[i] == ']')
            i++;
        if (CoolumnAbsolute && val == 0)
            CoolumnAbsolute = false;
        ColumnReference = CoolumnAbsolute ? val - 1 : val;
    }

    // Parse possibly signed decimal integer
    private static int ParseInt(string s, ref int i)
    {
        int val = 0;
        bool negative = false;
        if (i < s.Length && (s[i] == '-' || s[i] == '+'))
        {
            negative = s[i] == '-';
            i++;
        }
        val = 0;
        while (i < s.Length && char.IsDigit(s[i]))
        {
            val = val * 10 + (s[i] - '0');
            i++;
        }
        return negative ? -val : val;
    }

    private static bool IsAToZ(char c)
    {
        return 'a' <= c && c <= 'z' || 'A' <= c && c <= 'Z';
    }

    private static int AToZValue(char c)
    {
        return (c - 'A') % 32;
    }

    // Absolute address of ref 
    public CellAddr Addr(int col, int row)
    {
        return new CellAddr(this, col, row);
    }

    // Insert N new rowcols before rowcol R>=0, when we're at rowcol r
    public Adjusted<RARef> InsertRowCols(int R, int N, int r, bool insertRow)
    {
        int newRef;
        int upper;
        if (insertRow)
        { // Insert N rows before row R; we're at row r
            InsertRowCols(R, N, r, RowAbsolute, RowReference, out newRef, out upper);
            var rarefNew = new RARef(CoolumnAbsolute, ColumnReference, RowAbsolute, newRef);
            return new Adjusted<RARef>(rarefNew, upper, RowReference == newRef);
        }
        else
        { // Insert N columns before column R; we're at column r
            InsertRowCols(R, N, r, CoolumnAbsolute, ColumnReference, out newRef, out upper);
            var rarefNew = new RARef(CoolumnAbsolute, newRef, RowAbsolute, RowReference);
            return new Adjusted<RARef>(rarefNew, upper, ColumnReference == newRef);
        }
    }

    // Insert N new rowcols before rowcol R>=0, when we're at rowcol r
    private static void InsertRowCols(int R,
                                      int N,
                                      int r,
                                      bool rcAbs,
                                      int rcRef,
                                      out int newRc,
                                      out int upper)
    {
        if (rcAbs)
        {
            if (rcRef >= R)
            {
                // Absolute ref to cell after inserted          // Case (Ab)
                newRc = rcRef + N;
                upper = int.MaxValue;
            }
            else
            {
                // Absolute ref to cell before inserted       // Case (Aa) 
                newRc = rcRef;
                upper = int.MaxValue;
            }
        }
        else // Relative reference
            if (r >= R)
        {
            if (r + rcRef < R)
            {
                // Relative ref from after inserted rowcols to cell before them
                newRc = rcRef - N; // Case (Rab)
                upper = R - rcRef;
            }
            else
            {
                // Relative ref from after inserted rowcols to cell after them
                newRc = rcRef; // Case (Rbb)
                upper = int.MaxValue;
            }
        }
        else // r < R
                if (r + rcRef >= R)
        {
            // Relative ref from before inserted rowcols to cell after them
            newRc = rcRef + N; // Case (Rba)
            upper = R;
        }
        else
        {
            // Relative ref from before inserted rowcols to cell before them
            newRc = rcRef; // Case (Raa)
            upper = Math.Min(R, R - rcRef);
        }
    }

    // Clone and move (when the containing formula is moved, not copied)
    public RARef Move(int deltaCol, int deltaRow)
    {
        return new RARef(CoolumnAbsolute,
                         CoolumnAbsolute ? ColumnReference : ColumnReference - deltaCol,
                         RowAbsolute,
                         RowAbsolute ? RowReference : RowReference - deltaRow);
    }

    // Does this raref at (col, row) refer inside the sheet?
    public bool ValidAt(int col, int row)
    {
        var ca = new CellAddr(this, col, row);
        return 0 <= ca.Column && 0 <= ca.Row;
    }

    public string Show(int col, int row, Formats fo)
    {
        switch (fo.RefFmt)
        {
            case Formats.RefType.A1:
                var ca = new CellAddr(this, col, row);
                return (CoolumnAbsolute ? "$" : "") + CellAddr.ColumnName(ca.Column)
                       + (RowAbsolute ? "$" : "") + (ca.Row + 1);
            case Formats.RefType.R1C1:
                return "R" + RelAbsFormat(RowAbsolute, RowReference, 1)
                       + "C" + RelAbsFormat(CoolumnAbsolute, ColumnReference, 1);
            case Formats.RefType.C0R0:
                return "C" + RelAbsFormat(CoolumnAbsolute, ColumnReference, 0)
                       + "R" + RelAbsFormat(RowAbsolute, RowReference, 0);
            default:
                throw new InvalidOperationException("Unknown reference format");
        }
    }

    private static string RelAbsFormat(bool abs, int offset, int origo)
    {
        if (abs)
            return (offset + origo).ToString();
        else if (offset == 0)
            return string.Empty;
        else
            return "[" + (offset > 0 ? "+" : "") + offset.ToString() + "]";
    }

    public bool Equals(RARef that)
    {
        return that != null && CoolumnAbsolute == that.CoolumnAbsolute && RowAbsolute == that.RowAbsolute
               && ColumnReference == that.ColumnReference && RowReference == that.RowReference;
    }

    public override int GetHashCode()
    {
        return (((CoolumnAbsolute ? 1 : 0) + (RowAbsolute ? 2 : 0)) + ColumnReference * 4) * 37 + RowReference;
    }
}
