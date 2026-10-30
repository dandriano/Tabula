using System;
using System.Linq;
using System.Text;
using Tabula.CellAddressing;
using Tabula.Types;
using Tabula.Values;

namespace Tabula.Expressions;

/// <summary>
/// 
/// A FunCall expression is an operator application such as 1+$A$4 or a function
/// call such as RAND() or SIN(4*A$7) or SUM(B4:B52; 3) or IF(A1; A2; 1/A1).
/// </summary>
internal class FunCall : Calculation
{
    private readonly Function _function;
    private readonly Calculation[] _es;

    private FunCall(Function function, Calculation[] es)
    {
        ArgumentNullException.ThrowIfNull(function);
        ArgumentNullException.ThrowIfNull(es);
        _function = function;
        _es = es;
    }

    public static Calculation Make(string name, Calculation[] es)
    {
        var function = Function.Get(name);
        function ??= Function.MakeUnknown(name);
        for (int i = 0; i < es.Length; i++)
            es[i] ??= new Error("#SYNTAX");
        return new FunCall(function, es);
    }

    // Arguments are passed unevaluated to cater for non-strict IF 

    public override Value Eval(Sheet sheet, int col, int row)
    {
        return _function.Applier(sheet, _es, col, row);
    }

    public override Calculation Move(int deltaCol, int deltaRow)
    {
        var newEs = new Calculation[_es.Length];
        for (int i = 0; i < _es.Length; i++)
            newEs[i] = _es[i].Move(deltaCol, deltaRow);
        return new FunCall(_function, newEs);
    }

    // Can be copied with sharing if arguments can
    public override Calculation CopyTo(int col, int row)
    {
        var same = true;
        var newEs = new Calculation[_es.Length];
        for (int i = 0; i < _es.Length; i++)
        {
            newEs[i] = _es[i].CopyTo(col, row);
            same &= ReferenceEquals(newEs[i], _es[i]);
        }
        return same ? this : new FunCall(_function, newEs);
    }

    public override Adjusted<Calculation> InsertRowCols(Sheet modSheet,
                                                 bool thisSheet,
                                                 int R,
                                                 int N,
                                                 int r,
                                                 bool doRows)
    {
        var newEs = new Calculation[_es.Length];
        var upper = int.MaxValue;
        var same = true;
        for (int i = 0; i < _es.Length; i++)
        {
            Adjusted<Calculation> ae
                = _es[i].InsertRowCols(modSheet, thisSheet, R, N, r, doRows);
            upper = Math.Min(upper, ae.Upper);
            same = same && ae.Same;
            newEs[i] = ae.E;
        }
        return new Adjusted<Calculation>(new FunCall(_function, newEs), upper, same);
    }

    // Show infixed operators as infix and without excess parentheses

    public override string Show(int col, int row, int ctxpre, Formats fo)
    {
        var sb = new StringBuilder();
        var pre = _function.Fixity;
        if (pre == 0)
        { // Not operator
            sb.Append(_function.Name).Append('(');
            for (int i = 0; i < _es.Length; i++)
            {
                if (i > 0)
                    sb.Append(", ");
                sb.Append(_es[i].Show(col, row, 0, fo));
            }
            sb.Append(')');
        }
        else
        { // Operator.  Assume es.Length is 1 or 2 
            if (_es.Length == 2)
            {
                // If precedence lower than context, add parens
                if (pre < ctxpre)
                    sb.Append('(');
                sb.Append(_es[0].Show(col, row, pre, fo));
                sb.Append(_function.Name);
                // Only higher precedence right operands avoid parentheses
                sb.Append(_es[1].Show(col, row, pre + 1, fo));
                if (pre < ctxpre)
                    sb.Append(')');
            }
            else if (_es.Length == 1)
            {
                sb.Append(_function.Name == "NEG" ? "-" : _function.Name);
                sb.Append(_es[0].Show(col, row, pre, fo));
            }
            else
            {
                throw new InvalidOperationException("Operator not unary or binary");
            }
        }
        return sb.ToString();
    }

    internal override void VisitRefs(RefSet refSet, Action<CellRef> refAct, Action<CellArea> areaAct)
    {
        foreach (var e in _es)
            e.VisitRefs(refSet, refAct, areaAct);
    }

    public override void DependsOn(FullCellAddr here, Action<FullCellAddr> dependsOn)
    {
        foreach (var e in _es)
            e.DependsOn(here, dependsOn);
    }

    public override bool IsVolatile
    {
        get
        {
            if (_function.IsVolatile)
                return true;
            return _es.Any(e => e.IsVolatile);
        }
    }
}
