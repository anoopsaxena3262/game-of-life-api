using static GameOfLife.Domain.Tests.Boards;

namespace GameOfLife.Domain.Tests;

/// <summary>
/// Pins every field of <see cref="TerminationResult"/>: kind, the state at detection,
/// the generation where that state was first seen, the period, and how far the walk went.
/// </summary>
public sealed class TerminationDetectorTests
{
    private static readonly string Plus = Rows(
        "00000",
        "00100",
        "01110",
        "00100",
        "00000");

    private static readonly string PlusCycleState = Rows(
        "01110",
        "10001",
        "10001",
        "10001",
        "01110");

    [Fact]
    public void Still_life_is_detected_as_a_fixed_point_with_period_1()
    {
        var block = Rows(
            "0000",
            "0110",
            "0110",
            "0000");

        var result = Conclude(block, 4, 4, 10);

        Assert.Equal(new TerminationResult(TerminationKind.FixedPoint, block, 0, 1, 1), result);
    }

    [Fact]
    public void Blinker_is_detected_as_a_cycle_with_period_2()
    {
        var horizontal = Rows(
            "000",
            "111",
            "000");

        var result = Conclude(horizontal, 3, 3, 10);

        // Generation 2 returns to the start. Stillness-only detection would never stop.
        Assert.Equal(new TerminationResult(TerminationKind.Cycle, horizontal, 0, 2, 2), result);
    }

    [Fact]
    public void A_shared_hash_is_a_cycle_only_when_the_grids_match()
    {
        var horizontal = Rows(
            "000",
            "111",
            "000");

        var result = TerminationDetector.Detect(
            horizontal, 3, 3, 10, _ => new TerminationDetector.StateHash(1UL, 1UL));

        Assert.Equal(new TerminationResult(TerminationKind.Cycle, horizontal, 0, 2, 2), result);
    }

    [Fact]
    public void Board_that_dies_out_is_reported_as_extinct()
    {
        var single = Rows(
            "000",
            "010",
            "000");
        const string dead = "000000000";

        var result = Conclude(single, 3, 3, 10);

        // The dead board first appears at generation 1 and is confirmed on the next step.
        Assert.Equal(new TerminationResult(TerminationKind.Extinct, dead, 1, 1, 2), result);
    }

    [Fact]
    public void An_already_dead_board_is_extinct_at_generation_0()
    {
        var dead = Rows(
            "0000",
            "0000",
            "0000",
            "0000");

        var result = Conclude(dead, 4, 4, 10);

        Assert.Equal(new TerminationResult(TerminationKind.Extinct, dead, 0, 1, 1), result);
    }

    [Fact]
    public void A_still_life_reached_later_records_that_generation_not_zero()
    {
        var cornerBlock = Rows(
            "000000",
            "000000",
            "000000",
            "000000",
            "000011",
            "000011");

        var result = Conclude(Glider(), GliderSize, GliderSize, 100);

        // The corner block is generation 15; generation 16 only confirms it is stable.
        Assert.Equal(new TerminationResult(TerminationKind.FixedPoint, cornerBlock, 15, 1, 16), result);
    }

    [Fact]
    public void Extinction_after_a_collapse_records_when_the_dead_board_first_appeared()
    {
        var full = Rows(
            "1111",
            "1111",
            "1111",
            "1111");
        const string dead = "0000000000000000";

        var result = Conclude(full, 4, 4, 10);

        Assert.Equal(new TerminationResult(TerminationKind.Extinct, dead, 2, 1, 3), result);
    }

    [Fact]
    public void Returns_null_when_no_conclusion_is_reached_within_the_limit()
    {
        var horizontal = Rows(
            "000",
            "111",
            "000");

        // One step reaches the vertical phase, which has not been seen before.
        var unfinished = TerminationDetector.Detect(horizontal, 3, 3, 1);

        Assert.Null(unfinished);
    }

    [Fact]
    public void Reports_the_generation_where_the_cycle_first_occurred()
    {
        // Plus sign. Generations 0-3 are transient; a period-2 oscillator begins at generation 4.
        var result = Conclude(Plus, 5, 5, 20);

        Assert.Equal(new TerminationResult(TerminationKind.Cycle, PlusCycleState, 4, 2, 6), result);
    }

    [Fact]
    public void A_cycle_past_a_checkpoint_still_reports_the_original_entry_generation()
    {
        // Interval 2 stores generation 4, where this oscillator begins, and confirms
        // the repeat from that checkpoint rather than by replaying generation 0.
        var result = TerminationDetector.Detect(Plus, 5, 5, 20, checkpointInterval: 2);

        Assert.Equal(new TerminationResult(TerminationKind.Cycle, PlusCycleState, 4, 2, 6), result);
    }

    private static TerminationResult Conclude(string state, int width, int height, int maxGenerations)
    {
        var result = TerminationDetector.Detect(state, width, height, maxGenerations);
        Assert.NotNull(result);
        return result;
    }
}
