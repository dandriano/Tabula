namespace Tabula.Types;

/// <summary>
/// The recalculation state of a Formula cell.
/// </summary>
public enum FormulaCellState
{
    Dirty,
    Enqueued,
    Computing,
    Uptodate
}