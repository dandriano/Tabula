using System;
using System.Collections.Generic;
using System.Linq;

using Tabula.Cells;


namespace Tabula;

/// <summary>
/// A SheetRep represents a sheet's cell array sparsely, quadtree style, with four 
/// levels of tiles, each conceptually a 16-column by 32-row 2D array,
/// for up to 2^16 columns and 2^20 rows.
/// </summary>
internal class SheetRep : IEnumerable<Cell>
{
    // Sizes let address components be calculated with bit operations. Low bits
    // select a position within a tile; the remaining bits select the tile.

    private const int // Could be uint, but then indexes must be cast to int
        _logWidth = 4,
        _tileWidth = 1 << _logWidth,
        _maskWidth = _tileWidth - 1,
        _sizeWidth = 1 << (4 * _logWidth),
        // cols
        _logHeight = 5,
        _tileHeight = 1 << _logHeight,
        _maskHeight = _tileHeight - 1,
        _sizeHeight = 1 << (4 * _logHeight); // rows

    private readonly Cell[][][][] _tile0 = new Cell[_tileWidth * _tileHeight][][][];

    public Cell this[int c, int r]
    {
        get
        {
            if (c < 0 || _sizeWidth <= c || r < 0 || _sizeHeight <= r)
                return null;
            Cell[][][] tile1 = _tile0[(((c >> (3 * _logWidth)) & _maskWidth) << _logHeight) | ((r >> (3 * _logHeight)) & _maskHeight)];
            Cell[][] tile2 = tile1?[(((c >> (2 * _logWidth)) & _maskWidth) << _logHeight) | ((r >> (2 * _logHeight)) & _maskHeight)];
            Cell[] tile3 = tile2?[(((c >> (1 * _logWidth)) & _maskWidth) << _logHeight) | ((r >> (1 * _logHeight)) & _maskHeight)];
            return tile3?[((c & _maskWidth) << _logHeight) | (r & _maskHeight)];
        }
        set
        {
            if (c < 0 || _sizeWidth <= c || r < 0 || _sizeHeight <= r)
                return;
            int index0 = (((c >> (3 * _logWidth)) & _maskWidth) << _logHeight) | ((r >> (3 * _logHeight)) & _maskHeight);
            Cell[][][] tile1 = _tile0[index0];
            if (tile1 == null)
            {
                if (value == null)
                {
                    return;
                }
                else
                {
                    tile1 = _tile0[index0] = new Cell[_tileWidth * _tileHeight][][];
                }
            }
            int index1 = (((c >> (2 * _logWidth)) & _maskWidth) << _logHeight) | ((r >> (2 * _logHeight)) & _maskHeight);
            Cell[][] tile2 = tile1[index1];
            if (tile2 == null)
            {
                if (value == null)
                {
                    return;
                }
                else
                {
                    tile2 = tile1[index1] = new Cell[_tileWidth * _tileHeight][];
                }
            }
            int index2 = (((c >> (1 * _logWidth)) & _maskWidth) << _logHeight) | ((r >> (1 * _logHeight)) & _maskHeight);
            Cell[] tile3 = tile2[index2];
            if (tile3 == null)
            {
                if (value == null)
                {
                    return;
                }
                else
                {
                    tile3 = tile2[index2] = new Cell[_tileWidth * _tileHeight];
                }
            }
            int index3 = ((c & _maskWidth) << _logHeight) | (r & _maskHeight);
            tile3[index3] = value;
        }
    }

    // Yield all the sheet's non-null cells
    public IEnumerator<Cell> GetEnumerator()
    {
        return (from tile1 in _tile0
                where tile1 != null
                from tile2 in tile1
                where tile2 != null
                from tile3 in tile2
                where tile3 != null
                from cell in tile3
                where cell != null
                select cell).GetEnumerator();
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    // Sparse iteration, over non-null cells only
    public void Forall(Action<int, int, Cell> act)
    {
        int i0 = 0;
        foreach (Cell[][][] tile1 in _tile0)
        {
            int i1 = 0, c0 = (i0 >> _logHeight) << (3 * _logWidth), r0 = (i0 & _maskHeight) << (3 * _logHeight);
            if (tile1 != null)
            {
                foreach (Cell[][] tile2 in tile1)
                {
                    int i2 = 0, c1 = (i1 >> _logHeight) << (2 * _logWidth), r1 = (i1 & _maskHeight) << (2 * _logHeight);
                    if (tile2 != null)
                    {
                        foreach (Cell[] tile3 in tile2)
                        {
                            int i3 = 0, c2 = (i2 >> _logHeight) << (1 * _logWidth), r2 = (i2 & _maskHeight) << (1 * _logHeight);
                            if (tile3 != null)
                            {
                                foreach (Cell cell in tile3)
                                {
                                    if (cell != null)
                                    {
                                        act(c0 | c1 | c2 | i3 >> _logHeight, r0 | r1 | r2 | i3 & _maskHeight, cell);
                                    }
                                    i3++;
                                }
                            }
                            i2++;
                        }
                    }
                    i1++;
                }
            }
            i0++;
        }
    }
}
