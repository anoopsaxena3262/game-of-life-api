using GameOfLife.Domain;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GameOfLife.Application.Tests;

public sealed class BoardServiceTests
{
    private const string Horizontal = "000111000";
    private const string Vertical = "010010010";

    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-01-02T03:04:05Z");
    private static readonly CancellationToken None = CancellationToken.None;

    private readonly FakeBoardRepository _repository = new();

    [Fact]
    public async Task Rejects_a_grid_whose_dimensions_do_not_match_the_declared_width_and_height()
    {
        var service = Service(Limits(10, 100, 9));

        await Assert.ThrowsAsync<InvalidBoardException>(() => service.CreateAsync(0, 2, Grid(2, 2), None));
        await Assert.ThrowsAsync<InvalidBoardException>(() => service.CreateAsync(2, 0, Grid(1, 2), None));
        await Assert.ThrowsAsync<InvalidBoardException>(() => service.CreateAsync(2, 2, null, None));
        await Assert.ThrowsAsync<InvalidBoardException>(() => service.CreateAsync(2, 2, Grid(1, 2), None));
        await Assert.ThrowsAsync<InvalidBoardException>(() => service.CreateAsync(2, 2, [new bool[2], null], None));
        await Assert.ThrowsAsync<InvalidBoardException>(() => service.CreateAsync(2, 2, [new bool[2], new bool[1]], None));

        Assert.Empty(_repository.Saved);
    }

    [Fact]
    public async Task Rejects_a_board_exceeding_the_configured_cell_cap()
    {
        var service = Service(Limits(10, 100, 9));

        var error = await Assert.ThrowsAsync<InvalidBoardException>(() => service.CreateAsync(4, 3, Grid(3, 4), None));

        Assert.Contains("9", error.Message);
        Assert.Empty(_repository.Saved);
    }

    [Fact]
    public async Task Generation_0_returns_the_uploaded_board_unchanged()
    {
        var service = Service(Limits(10, 100, 9));

        var id = await service.CreateAsync(3, 3, Blinker(), None);

        var board = Assert.Single(_repository.Saved);
        Assert.Equal(new Board(id, 3, 3, Horizontal, Now, null), board);

        // Without a generation 0 row the service falls back to the stored initial state.
        _repository.Generations.Clear();
        Assert.Equal(Horizontal, await service.GenerationAtAsync(id, 0, None));
        Assert.Empty(_repository.SavedGenerations);
    }

    [Fact]
    public async Task Repeated_reads_of_the_same_generation_return_the_same_state()
    {
        var service = Service(Limits(10, 100, 9));
        var id = Seed(null);

        var first = await service.GenerationAtAsync(id, 1, None);
        var second = await service.GenerationAtAsync(id, 1, None);

        Assert.Equal(Vertical, first);
        Assert.Equal(first, second);
        Assert.Equal(Vertical, _repository.Generations[(id, 1)]);
        Assert.Single(_repository.SavedGenerations);
    }

    [Fact]
    public async Task A_cached_generation_is_served_without_recomputation()
    {
        var service = Service(Limits(10, 100, 9));
        var id = Guid.NewGuid();
        _repository.Generations[(id, 1)] = Vertical;

        Assert.Equal(Vertical, await service.GenerationAtAsync(id, 1, None));

        Assert.Empty(_repository.SavedGenerations);
        Assert.Equal(0, _repository.FindHighestCachedIndexCalls);
        Assert.Equal(0, _repository.FindByIdCalls);
    }

    [Fact]
    public async Task Resumes_from_the_highest_cached_generation_rather_than_from_zero()
    {
        var service = Service(Limits(10, 100, 9));
        var id = Seed(null);
        _repository.Generations[(id, 1)] = Vertical;

        Assert.Equal(Horizontal, await service.GenerationAtAsync(id, 2, None));

        Assert.Equal([(id, 2, Horizontal)], _repository.SavedGenerations);
    }

    [Fact]
    public async Task A_gap_below_the_highest_cached_index_is_not_answered_with_that_later_state()
    {
        var service = Service(Limits(10, 100, 9));
        var id = Seed(null);
        _repository.Generations[(id, 4)] = "111111111";

        Assert.Equal(Vertical, await service.GenerationAtAsync(id, 1, None));

        Assert.Equal([(id, 1, Vertical)], _repository.SavedGenerations);
    }

    [Fact]
    public async Task Clamps_a_caller_supplied_generation_limit_to_the_configured_ceiling()
    {
        var service = Service(Limits(1, 1, 9));
        var id = Seed(null);

        var error = await Assert.ThrowsAsync<NoConclusionException>(() => service.FinalStateAsync(id, 50, None));

        Assert.Equal(1, error.GenerationsAttempted);
    }

    [Fact]
    public async Task Rejects_a_non_positive_max_generations_instead_of_reporting_422()
    {
        var service = Service(Limits(10, 100, 9));
        var id = Seed(null);

        await Assert.ThrowsAsync<InvalidBoardException>(() => service.FinalStateAsync(id, 0, None));
        await Assert.ThrowsAsync<InvalidBoardException>(() => service.FinalStateAsync(id, -3, None));
    }

    [Fact]
    public async Task Uses_the_board_limit_when_the_caller_does_not_supply_one()
    {
        var service = Service(Limits(10, 100, 9));
        var id = Seed(1);

        var error = await Assert.ThrowsAsync<NoConclusionException>(() => service.FinalStateAsync(id, null, None));

        Assert.Equal(1, error.GenerationsAttempted);
    }

    [Fact]
    public async Task Uses_the_configured_default_and_returns_a_conclusion()
    {
        var service = Service(Limits(10, 100, 9));
        var id = Seed(null);

        var outcome = await service.FinalStateAsync(id, null, None);

        Assert.Equal(TerminationKind.Cycle, outcome.Result.Kind);
        Assert.Equal(2, outcome.Result.Period);
        Assert.Equal(10, outcome.GenerationsLimit);
    }

    [Fact]
    public async Task Rejects_a_negative_index_and_an_index_past_the_ceiling()
    {
        var service = Service(Limits(10, 100, 9));
        var id = Guid.NewGuid();

        await Assert.ThrowsAsync<InvalidBoardException>(() => service.GenerationAtAsync(id, -1, None));
        await Assert.ThrowsAsync<InvalidBoardException>(() => service.GenerationAtAsync(id, 101, None));

        Assert.Empty(_repository.GenerationLookups);
    }

    [Fact]
    public async Task Missing_board_and_missing_resume_point()
    {
        var service = Service(Limits(10, 100, 9));
        var missing = Guid.NewGuid();
        await Assert.ThrowsAsync<BoardNotFoundException>(() => service.GetAsync(missing, None));

        var id = Seed(null);
        _repository.HighestCachedIndexOverride = 1;

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GenerationAtAsync(id, 2, None));
    }

    [Fact]
    public async Task Rejects_a_walk_whose_cells_times_generations_exceed_the_budget()
    {
        var service = Service(Limits(10, 100, 9, cellGenerations: 9));
        var id = Seed(null);

        var error = await Assert.ThrowsAsync<InvalidBoardException>(() => service.GenerationAtAsync(id, 2, None));

        Assert.Contains("budget", error.Message);
        Assert.Empty(_repository.SavedGenerations);
    }

    [Fact]
    public async Task Final_state_uses_the_generation_cap_not_the_cell_generation_budget()
    {
        var service = Service(Limits(10, 100, 9, cellGenerations: 9));
        var id = Seed(null);

        var outcome = await service.FinalStateAsync(id, null, None);

        Assert.Equal(10, outcome.GenerationsLimit);
        Assert.Equal(TerminationKind.Cycle, outcome.Result.Kind);
    }

    [Fact]
    public async Task Get_returns_the_stored_board()
    {
        var service = Service(Limits(10, 100, 9));
        var id = Seed(null);

        Assert.Equal(_repository.Boards[id], await service.GetAsync(id, None));
    }

    private BoardService Service(GameOfLifeOptions limits) =>
        new(_repository, Options.Create(limits), new FixedTimeProvider(Now), NullLogger<BoardService>.Instance);

    /// <summary>Stores a horizontal blinker and its generation 0, as an upload would.</summary>
    private Guid Seed(int? maxGenerations)
    {
        var id = Guid.NewGuid();
        _repository.Boards[id] = new Board(id, 3, 3, Horizontal, Now, maxGenerations);
        _repository.Generations[(id, 0)] = Horizontal;
        return id;
    }

    private static GameOfLifeOptions Limits(int maxGenerations, int ceiling, int maxCells, long cellGenerations = 1_000_000) =>
        new()
        {
            MaxGenerations = maxGenerations,
            MaxGenerationsCeiling = ceiling,
            MaxCells = maxCells,
            MaxCellGenerations = cellGenerations,
            MaxRequestBytes = 2_000_000,
        };

    private static bool[][] Grid(int rows, int cols) =>
        Enumerable.Range(0, rows).Select(_ => new bool[cols]).ToArray();

    private static bool[][] Blinker() =>
    [
        [false, false, false],
        [true, true, true],
        [false, false, false],
    ];
}
