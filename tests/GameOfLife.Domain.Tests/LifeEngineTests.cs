using static GameOfLife.Domain.Tests.Boards;

namespace GameOfLife.Domain.Tests;

/// <summary>The rules, exercised with no host, database or serializer.</summary>
public sealed class LifeEngineTests
{
    [Fact]
    public void Block_is_a_still_life_unchanged_after_a_step()
    {
        var block = Rows(
            "0000",
            "0110",
            "0110",
            "0000");

        Assert.Equal(block, LifeEngine.Step(block, 4, 4));
        Assert.Equal(block, Advance(block, 4, 4, 5));
    }

    [Fact]
    public void Blinker_oscillates_with_period_2()
    {
        var horizontal = Rows(
            "000",
            "111",
            "000");
        var vertical = Rows(
            "010",
            "010",
            "010");

        Assert.Equal(vertical, LifeEngine.Step(horizontal, 3, 3));
        Assert.Equal(horizontal, Advance(horizontal, 3, 3, 2));
    }

    [Fact]
    public void Toad_oscillates_with_period_2()
    {
        var phaseA = Rows(
            "000000",
            "001110",
            "011100",
            "000000");
        var phaseB = Rows(
            "000100",
            "010010",
            "010010",
            "001000");

        Assert.Equal(phaseB, LifeEngine.Step(phaseA, 6, 4));
        Assert.Equal(phaseA, Advance(phaseA, 6, 4, 2));
    }

    [Fact]
    public void Beacon_oscillates_with_period_2()
    {
        var phaseA = Rows(
            "1100",
            "1100",
            "0011",
            "0011");
        var phaseB = Rows(
            "1100",
            "1000",
            "0001",
            "0011");

        Assert.Equal(phaseB, LifeEngine.Step(phaseA, 4, 4));
        Assert.Equal(phaseA, Advance(phaseA, 4, 4, 2));
    }

    [Fact]
    public void Glider_shifts_one_cell_down_and_across_every_four_generations()
    {
        var shiftedOnce = Rows(
            "000000",
            "001000",
            "000100",
            "011100",
            "000000",
            "000000");
        var shiftedTwice = Rows(
            "000000",
            "000000",
            "000100",
            "000010",
            "001110",
            "000000");

        Assert.Equal(shiftedOnce, Advance(Glider(), GliderSize, GliderSize, 4));
        Assert.Equal(shiftedTwice, Advance(Glider(), GliderSize, GliderSize, 8));
    }

    [Fact]
    public void Glider_dies_in_the_corner_and_does_not_reappear_on_the_opposite_edge()
    {
        // Same southeast glider. On a torus, generation 13 puts a live cell on the top edge.
        var atImpact = Advance(Glider(), GliderSize, GliderSize, 13);
        Assert.Equal("000000", Edge(atImpact, 0));
        for (var row = 0; row < GliderSize; row++)
        {
            Assert.Equal('0', atImpact[row * GliderSize]);
        }

        var cornerBlock = Rows(
            "000000",
            "000000",
            "000000",
            "000000",
            "000011",
            "000011");
        var settled = Advance(Glider(), GliderSize, GliderSize, 16);
        Assert.Equal(cornerBlock, settled);
        Assert.Equal(settled, LifeEngine.Step(settled, GliderSize, GliderSize));
        Assert.Equal("000000", Edge(settled, 0));
    }

    [Fact]
    public void Empty_board_stays_empty()
    {
        var empty = Rows(
            "0000",
            "0000",
            "0000",
            "0000");

        Assert.Equal(empty, LifeEngine.Step(empty, 4, 4));
        Assert.True(StateCodec.IsExtinct(LifeEngine.Step(empty, 4, 4)));
    }

    [Fact]
    public void Single_live_cell_dies_of_underpopulation()
    {
        var center = Rows(
            "000",
            "010",
            "000");
        var corner = Rows(
            "100",
            "000",
            "000");
        const string dead = "000000000";

        Assert.Equal(dead, LifeEngine.Step(center, 3, 3));
        Assert.Equal(dead, LifeEngine.Step(corner, 3, 3));
        Assert.Equal("0", LifeEngine.Step("1", 1, 1));
    }

    [Fact]
    public void Fully_live_board_collapses_to_its_four_corners()
    {
        var full = Rows(
            "1111",
            "1111",
            "1111",
            "1111");
        var corners = Rows(
            "1001",
            "0000",
            "0000",
            "1001");

        Assert.Equal(corners, LifeEngine.Step(full, 4, 4));
        Assert.Equal("0000000000000000", Advance(full, 4, 4, 2));
    }

    [Fact]
    public void One_by_one_and_one_by_n_grids_step_without_error()
    {
        Assert.Equal("0", LifeEngine.Step("0", 1, 1));
        Assert.Equal("0", LifeEngine.Step("1", 1, 1));

        Assert.Equal("01110", LifeEngine.Step("11111", 5, 1));
        Assert.Equal("01110", LifeEngine.Step("11111", 1, 5));
        Assert.Equal("00000", LifeEngine.Step("00000", 1, 5));
        Assert.Equal("000", LifeEngine.Step("010", 3, 1));
    }

    private static string Edge(string state, int row) => state.Substring(row * GliderSize, GliderSize);
}
