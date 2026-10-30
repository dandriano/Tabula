using System;

using Tabula.CellAddressing;


namespace Tabula;

/// <summary>
/// An IDepend is an object such as Cell or Calculation node that can report
/// the full cell addresses it depends on.
/// </summary>
public interface IDepend
{
    void DependsOn(FullCellAddr here, Action<FullCellAddr> dependsOn);
}
