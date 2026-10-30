# Tabula

Tabula is a spreadsheet implementation in C#. A workbook contains sheets; sheets hold cells; formulas are parsed into calculation trees and evaluated on demand. The support graph records which cells depend on each cell so recalculation can focus on the affected part of a workbook. Shout out Peter Sestoft.

## Core objects

```text
Workbook
  └── Sheet (one per worksheet)
        └── SheetRep (sparse cell storage)
              └── Cell at (column, row)
                    ├── constant cell
                    ├── formula cell ── Calculation tree
                    └── array formula cell ── shared cached array formula
```

`SheetRep` stores populated cells sparsely. A `Cell` is the common base for constants and formulas. A formula cell holds its calculation tree, cached result, and evaluation state. The calculation tree represents the formula's operations and references.

## Addresses and references

Sheet coordinates are zero-based `(column, row)` pairs. `CellAddr` is a cell position; `FullCellAddr` adds the sheet. Formula references use `RARef`, which records whether each coordinate is absolute or relative to the formula's position.

```text
Formula position + RARef  ──resolve──▶  CellAddr  ──with sheet──▶  FullCellAddr

Example: copied formula =A1 in B1
  B1's relative reference points one column left, same row  ──▶ A1
  copied to B2, it points one column left, same row         ──▶ A2
```

This keeps a copied formula's reference meaning independent of its final address. The main types are `CellAddr`, `FullCellAddr`, and `RARef`.

## Support graph

The support graph stores the reverse of formula references: an edge goes from a referenced cell to a cell whose formula uses it. Each cell's `SupportSet` lists the cells it supports (its dependents).

For example, if `C1 = A1 + B1` and `D1 = C1 * 2`:

```text
A1 ─┐
    ├──▶ C1 ──▶ D1
B1 ─┘

SupportSet(A1) = { C1 }
SupportSet(B1) = { C1 }
SupportSet(C1) = { D1 }
```

The arrow direction is useful for recalculation: after `A1` changes, Tabula follows arrows to find formulas that may need new values.

### Building and storing support sets

When the workbook rebuilds the graph, it clears existing support sets and scans the sheets. Formula references and areas add the formula cells that depend on them to the referenced cells' sets. The sheet also looks for rectangular blocks of formulas that share the same calculation tree and processes each block together. Relative and absolute references are mapped across the block.

Support sets can hold either a single supported cell or a rectangular area, instead of requiring every dependent coordinate to be stored as a separate range. For example, if many formulas refer to the same absolute cell, their dependent cells can be represented as an area:

```text
Formula cells: C1:C100 each refer to $A$1

SupportSet(A1) = [C1:C100]
```

The implementation is in `Workbook.RebuildSupportGraph`, `Sheet.AddToSupportSets`, `Calculation`, `SupportSet`, and `SupportArea`.

### Recalculating affected formulas

When support metadata is valid, recalculation starts from edited and volatile cells. It marks their reachable dependents dirty, queues the roots, and evaluates queued formulas. Evaluating a dirty formula enqueues its dependents, extending the traversal through the affected part of the graph.

```text
Edited / volatile roots
          │
          ▼
 Follow support edges and mark formulas dirty
          │
          ▼
 Enqueue roots, then evaluate queued formulas
          │
          └── each evaluated formula enqueues its dependents
```

For example, editing `A1` in the graph above marks `C1` and `D1` dirty. Other formulas that cannot be reached from `A1` do not need recalculation. Formula evaluation still reads its inputs through the calculation tree, evaluating dependencies as needed.

Changing formula contents invalidates support metadata, so the next recalculation rebuilds the graph. When a cyclic dependency is found, the workbook falls back to full recalculation on the next pass. See `Workbook.Recalculate` and `Formula`.