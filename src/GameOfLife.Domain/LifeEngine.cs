namespace GameOfLife.Domain;

/// <summary>
/// The Conway rules, B3/S23, on a finite grid with dead borders.
/// </summary>
/// <remarks>
/// Out-of-bounds neighbours count as dead; the grid does not wrap. A glider therefore
/// reaches the edge and decays rather than travelling forever.
/// </remarks>
public static class LifeEngine
{
    /// <summary>Advances a flat state by exactly one generation.</summary>
    /// <param name="state">Flat row-major '0'/'1' string of length <paramref name="width"/> * <paramref name="height"/>.</param>
    /// <returns>The next generation in the same encoding.</returns>
    public static string Step(string state, int width, int height)
    {
        var grid = StateCodec.Deserialize(state, width, height);
        var next = Step(grid, width, height);
        return StateCodec.Serialize(next);
    }

    /// <summary>
    /// Advances a grid by one generation into a fresh array. The input is never changed;
    /// the caller may still be holding it.
    /// </summary>
    internal static bool[][] Step(bool[][] current, int width, int height)
    {
        // B3/S23, written into a new grid so the caller can keep `current`.
        //   live and 2 or 3 neighbours -> live
        //   dead and exactly 3         -> live
        //   anything else              -> dead
        var next = new bool[height][];
        for (var row = 0; row < height; row++)
        {
            next[row] = new bool[width];
            for (var col = 0; col < width; col++)
            {
                var liveNeighbours = CountLiveNeighbours(current, row, col, width, height);
                var isAlive = current[row][col];
                next[row][col] = isAlive
                    ? liveNeighbours is 2 or 3
                    : liveNeighbours == 3;
            }
        }

        return next;
    }

    /// <summary>
    /// Counts live cells among the eight neighbours of (<paramref name="row"/>, <paramref name="col"/>).
    /// Positions outside the grid count as dead.
    /// </summary>
    internal static int CountLiveNeighbours(bool[][] grid, int row, int col, int width, int height)
    {
        var count = 0;
        for (var dr = -1; dr <= 1; dr++)
        {
            for (var dc = -1; dc <= 1; dc++)
            {
                if (dr == 0 && dc == 0)
                {
                    continue;
                }

                var nr = row + dr;
                var nc = col + dc;
                // Off the board counts as dead. The grid does not wrap.
                if (nr >= 0 && nr < height && nc >= 0 && nc < width && grid[nr][nc])
                {
                    count++;
                }
            }
        }

        return count;
    }
}
