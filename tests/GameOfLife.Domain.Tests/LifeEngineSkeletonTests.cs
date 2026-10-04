using GameOfLife.Domain;

namespace GameOfLife.Domain.Tests;

public sealed class LifeEngineSkeletonTests
{
    private const string Later = "Skeleton. The assertion is written with the rule it covers.";

    [Fact]
    public void New_board_id_is_a_version_7_guid()
    {
        var id = BoardId.New();

        Assert.Equal(7, id.Value.Version);
    }

    [Fact]
    public void Grid_records_its_dimensions()
    {
        var grid = new CellGrid(5, 3);

        Assert.Equal(5, grid.Width);
        Assert.Equal(3, grid.Height);
    }

    [Fact(Skip = Later)]
    public void Block_is_unchanged()
    {
    }

    [Fact(Skip = Later)]
    public void Blinker_flips_axis()
    {
    }

    [Fact(Skip = Later)]
    public void Single_live_cell_dies()
    {
    }

    [Fact(Skip = Later)]
    public void Fixed_point_reports_the_generation()
    {
    }

    [Fact(Skip = Later)]
    public void Periodic_board_reports_period_start_and_length()
    {
    }

    [Fact(Skip = Later)]
    public void Indeterminate_board_reports_generations_computed()
    {
    }

    [Fact(Skip = Later)]
    public void Advance_composes()
    {
    }
}
