namespace Tabula.Cells;

/// <summary>
/// Reference notation used by formulas passed to <see cref="Cell.Parse(string, Workbook, int, int, ReferenceStyle)"/>.
/// </summary>
public enum ReferenceStyle
{
    A1,
    R1C1
}
