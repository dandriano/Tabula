using Irony.Parsing;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Tabula.CellAddressing;
using Tabula.Cells;
using Tabula.Types;
using Tabula.Values;
using XLParser;


namespace Tabula.Expressions;

/// <summary>
/// Converts XLParser's Excel formula syntax tree to the engine expression model.
/// </summary>
internal static class ExcelFormulaAdapter
{
    internal static Calculation Parse(string formula, Workbook workbook, int col, int row,
                                      ReferenceStyle style)
    {
        ParseTree tree;
        try
        {
            string formulaText = style == ReferenceStyle.R1C1
                ? NormalizeR1C1References(formula, col, row)
                : formula;
            tree = ExcelFormulaParser.ParseToTree(formulaText);
        }
        catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException || ex is FormatException)
        {
            return null;
        }
        if (tree?.Root == null || tree.ParserMessages.Any(message => message.Level.ToString() == "Error"))
        {
            return null;
        }
        try
        {
            return Convert(tree.Root, workbook, col, row);
        }
        catch (UnsupportedFormulaException ex)
        {
            return new Error(ErrorValue.Make("#UNSUPPORTED: " + ex.Message));
        }
        catch (Exception ex) when (ex is FormatException || ex is OverflowException || ex is ArgumentException)
        {
            return new Error(ErrorValue.Make("#UNSUPPORTED: " + ex.Message));
        }
    }

    private static string NormalizeR1C1References(string formula, int col, int row)
    {
        // XLParser accepts A1 references. Convert R1C1 tokens outside string
        // literals first, retaining RARef's canonical relative/absolute form.
        const string pattern = @"(?i)(?<![A-Z0-9_.])R(?:\[[+-]?\d+\]|[+-]?\d+)?C(?:\[[+-]?\d+\]|[+-]?\d+)?(?![A-Z0-9_.!])";
        var reference = new Regex(pattern, RegexOptions.CultureInvariant);
        var sheetPrefixes = new List<string>();
        string protectedFormula = Regex.Replace(formula, @"'(?:[^']|'')+'!", match =>
        {
            string marker = "\u001FTABULA_SHEET_PREFIX_" + sheetPrefixes.Count + "\u001F";
            sheetPrefixes.Add(match.Value);
            return marker;
        });
        var output = new StringBuilder(protectedFormula.Length);
        int segmentStart = 0;
        bool inString = false;
        for (int i = 0; i < protectedFormula.Length; i++)
        {
            if (protectedFormula[i] != '"')
                continue;
            if (inString && i + 1 < protectedFormula.Length && protectedFormula[i + 1] == '"')
            {
                i++;
                continue;
            }
            string segment = protectedFormula.Substring(segmentStart, i - segmentStart);
            output.Append(inString ? segment : ConvertSegment(segment));
            output.Append('"');
            inString = !inString;
            segmentStart = i + 1;
        }
        string tail = protectedFormula.Substring(segmentStart);
        output.Append(inString ? tail : ConvertSegment(tail));
        string normalized = output.ToString();
        for (int i = 0; i < sheetPrefixes.Count; i++)
            normalized = normalized.Replace("\u001FTABULA_SHEET_PREFIX_" + i + "\u001F", sheetPrefixes[i], StringComparison.Ordinal);
        return normalized;

        string ConvertSegment(string segment) => reference.Replace(segment, match =>
            new RARef(match.Value.ToUpperInvariant())
                .Show(col, row, new Formats { RefFmt = Formats.RefType.A1 }));
    }

    private static Calculation Convert(ParseTreeNode node, Workbook workbook, int col, int row)
    {
        string name = node.Term.Name;
        if (name == "FormulaWithEq" || name == "Formula")
        {
            var child = node.ChildNodes.FirstOrDefault(n => n.Term.Name != "=");
            return child == null ? Unsupported("empty expression") : Convert(child, workbook, col, row);
        }
        if (name == "Reference")
            return ConvertReference(node, workbook, col, row);
        if (name == "Constant")
            return Convert(node.ChildNodes.First(), workbook, col, row);
        if (name == "Number")
        {
            var token = FindToken(node, "NumberToken");
            return new NumberConst(double.Parse(token.Token.Text, CultureInfo.InvariantCulture));
        }
        if (name == "Text")
        {
            var token = FindToken(node, "TextToken");
            string value = token.Token.Text;
            if (value.Length >= 2)
                value = value.Substring(1, value.Length - 2);
            return new TextConst(value.Replace("\"\"", "\""));
        }
        if (name == "Bool")
        {
            var token = FindToken(node, "BoolToken");
            return new NumberConst(token.Token.Text.Equals("TRUE", StringComparison.OrdinalIgnoreCase) ? 1 : 0);
        }
        if (name == "Error")
        {
            var token = FindToken(node, "ErrorToken");
            return new Error(ErrorValue.Make(token.Token.Text));
        }
        if (name == "FunctionCall" || name == "ReferenceFunctionCall" || name == "UDFunctionCall")
        {
            if (TryOperator(node, out string op, out ParseTreeNode left, out ParseTreeNode right))
            {
                if (op == "%")
                    return FunCall.Make("/", new Calculation[]
                    { Convert(left, workbook, col, row), new NumberConst(100) });
                return FunCall.Make(op, new[]
                { Convert(left, workbook, col, row), Convert(right, workbook, col, row) });
            }
            if (TryUnary(node, out string unary, out ParseTreeNode operand))
            {
                return unary == "+" ? Convert(operand, workbook, col, row) : FunCall.Make("NEG", new[]
                { Convert(operand, workbook, col, row) });
            }
            if (TryPostfixPercent(node, out ParseTreeNode percentage))
            {
                return FunCall.Make("/", new Calculation[]
                { Convert(percentage, workbook, col, row), new NumberConst(100) });
            }
            if (ExcelFormulaParser.IsFunction(node))
            {
                string function = ExcelFormulaParser.GetFunction(node).ToUpperInvariant();
                var argumentsNode = node.ChildNodes.FirstOrDefault(n => n.Term.Name == "Arguments");
                Calculation[] args = argumentsNode == null
                    ? Array.Empty<Calculation>()
                    : argumentsNode.ChildNodes.Where(n => n.Term.Name == "Argument")
                        .Select(argument => Convert(argument.ChildNodes.First(), workbook, col, row)).ToArray();
                return FunCall.Make(function, args);
            }
        }
        if (node.ChildNodes.Count == 1)
            return Convert(node.ChildNodes[0], workbook, col, row);
        return Unsupported("unsupported syntax " + name);
    }

    private static Calculation ConvertReference(ParseTreeNode node, Workbook workbook, int col, int row)
    {
        var range = node.ChildNodes.FirstOrDefault(child => child.Term.Name == "ReferenceFunctionCall");
        if (range != null && range.ChildNodes.Count >= 3 && range.ChildNodes[1].Token?.Text == ":")
        {
            var first = ReadCellReference(range.ChildNodes[0], workbook, col, row);
            var last = ReadCellReference(range.ChildNodes[2], workbook, col, row);
            if (first.Error != null)
                return first.Error;
            if (last.Error != null)
                return last.Error;
            var outerSheet = ReadSheet(node, workbook, out Error outerError);
            if (outerError != null)
                return outerError;
            var firstSheet = first.Sheet ?? outerSheet;
            var lastSheet = last.Sheet ?? outerSheet;
            if (firstSheet != null && lastSheet != null && firstSheet != lastSheet)
                return Unsupported("a range across two worksheets");
            return new CellArea(firstSheet ?? lastSheet, first.Reference, last.Reference);
        }
        var call = node.ChildNodes.FirstOrDefault(child => ExcelFormulaParser.IsFunction(child) && ExcelFormulaParser.GetFunction(child) != ":");
        if (call != null)
            return Convert(call, workbook, col, row);
        var cell = ReadCellReference(node, workbook, col, row);
        return cell.Error != null ? cell.Error : new CellRef(cell.Sheet, cell.Reference);
    }

    private static CellReference ReadCellReference(ParseTreeNode node, Workbook workbook, int col, int row)
    {
        var token = FindTokenOrNull(node, "CellToken");
        if (token == null)
            return CellReference.Invalid();
        var sheet = ReadSheet(node, workbook, out Error sheetError);
        if (sheetError != null)
            return CellReference.WithError(sheetError);
        return new CellReference(sheet, new RARef(token.Token.Text, col, row));
    }

    private static Sheet ReadSheet(ParseTreeNode node, Workbook workbook, out Error error)
    {
        error = null;
        var prefix = FindNode(node, "Prefix");
        if (prefix == null)
            return null;
        var sheetToken = prefix.ChildNodes.SelectMany(Flatten).FirstOrDefault(n => n.Term.Name == "SheetNameToken" || n.Term.Name == "SheetNameQuotedToken");
        if (sheetToken == null)
            return null;
        string sheetName = sheetToken.Token.Text.TrimEnd('!');
        if (sheetToken.Term.Name == "SheetNameQuotedToken")
            sheetName = sheetName.TrimEnd('\'');
        var sheet = workbook[sheetName];
        if (sheet == null)
            error = new Error(ErrorValue.RefError);
        return sheet;
    }

    private static bool TryOperator(ParseTreeNode node, out string op, out ParseTreeNode left, out ParseTreeNode right)
    {
        op = null;
        left = right = null;
        if (node.ChildNodes.Count != 3)
            return false;
        var token = node.ChildNodes[1].Token;
        if (token == null || !new[]
        { "+", "-", "*", "/", "^", "&", "=", "<>", "<", "<=", ">", ">=" }.Contains(token.Text))
            return false;
        op = token.Text;
        left = node.ChildNodes[0];
        right = node.ChildNodes[2];
        return true;
    }

    private static bool TryUnary(ParseTreeNode node, out string op, out ParseTreeNode operand)
    {
        op = null;
        operand = null;
        if (node.ChildNodes.Count != 2 || node.ChildNodes[0].Token == null)
            return false;
        string candidate = node.ChildNodes[0].Token.Text;
        if (candidate != "+" && candidate != "-")
            return false;
        op = candidate;
        operand = node.ChildNodes[1];
        return true;
    }

    private static bool TryPostfixPercent(ParseTreeNode node, out ParseTreeNode operand)
    {
        operand = null;
        if (node.ChildNodes.Count != 2 || node.ChildNodes[1].Token?.Text != "%")
            return false;
        operand = node.ChildNodes[0];
        return true;
    }

    private static ParseTreeNode FindNode(ParseTreeNode node, string term) => Flatten(node).FirstOrDefault(n => n.Term.Name == term);
    private static ParseTreeNode FindToken(ParseTreeNode node, string term) => FindTokenOrNull(node, term) ?? throw new UnsupportedFormulaException("missing " + term);
    private static ParseTreeNode FindTokenOrNull(ParseTreeNode node, string term) => Flatten(node).FirstOrDefault(n => n.Term.Name == term && n.Token != null);
    private static IEnumerable<ParseTreeNode> Flatten(ParseTreeNode node)
    {
        yield return node;
        foreach (var child in node.ChildNodes)
            foreach (var nested in Flatten(child))
                yield return nested;
    }
    private static Calculation Unsupported(string syntax) => throw new UnsupportedFormulaException(syntax);

    private sealed class UnsupportedFormulaException : Exception
    {
        internal UnsupportedFormulaException(string message) : base(message)
        {
        }
    }
    private sealed class CellReference
    {
        internal readonly Sheet Sheet;
        internal readonly RARef Reference;
        internal readonly Error Error;
        internal CellReference(Sheet sheet, RARef reference)
        {
            Sheet = sheet;
            Reference = reference;
        }
        private CellReference(Error error)
        {
            Error = error;
        }
        internal static CellReference Invalid() => WithError(new Error(ErrorValue.Make("#UNSUPPORTED: non-cell reference")));
        internal static CellReference WithError(Error error) => new CellReference(error);
    }
}
