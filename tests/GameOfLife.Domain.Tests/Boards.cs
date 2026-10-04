using System.Text;

namespace GameOfLife.Domain.Tests;

/// <summary>Builds flat states from readable rows of '0' and '1'.</summary>
internal static class Boards
{
    public const int GliderSize = 6;

    /// <summary>Southeast glider in the top-left of a 6x6 board.</summary>
    public static string Glider() => Rows(
        "010000",
        "001000",
        "111000",
        "000000",
        "000000",
        "000000");

    public static string Rows(params string[] lines)
    {
        var width = lines[0].Length;
        var board = new StringBuilder(width * lines.Length);
        foreach (var line in lines)
        {
            if (line.Length != width || line.Any(c => c is not ('0' or '1')))
            {
                throw new ArgumentException($"row must be {width} of 0/1: {line}");
            }

            board.Append(line);
        }

        return board.ToString();
    }

    public static string Advance(string state, int width, int height, int generations)
    {
        var current = state;
        for (var i = 0; i < generations; i++)
        {
            current = LifeEngine.Step(current, width, height);
        }

        return current;
    }
}
