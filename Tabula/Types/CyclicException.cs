using System;
using Tabula.CellAddressing;

namespace Tabula.Types;

/// <summary>
/// A CyclicException signals that a cyclic dependency is discovered 
/// during evaluation.
/// </summary>
public class CyclicException : Exception
{
    public FullCellAddr Culprit { get; }

    public CyclicException(string msg, FullCellAddr culprit) : base(msg)
    {
        Culprit = culprit;
    }
}