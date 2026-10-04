using System.Text.Json;
using GameOfLife.Api.Problems;
using GameOfLife.Application;

namespace GameOfLife.Api.IntegrationTests;

/// <summary>Each exception-to-problem mapping, without a host.</summary>
public sealed class ApiProblemsTests
{
    [Fact]
    public void Missing_board_is_404_with_a_title()
    {
        var problem = ApiProblems.From(new BoardNotFoundException(Guid.NewGuid()))!;

        Assert.Equal(404, problem.Status);
        Assert.Equal("Board not found", problem.Title);
        Assert.Contains("No board", problem.Detail);
    }

    [Fact]
    public void Invalid_board_is_400_with_a_title()
    {
        var problem = ApiProblems.From(new InvalidBoardException("Width and height must be positive"))!;

        Assert.Equal(400, problem.Status);
        Assert.Equal("Invalid board", problem.Title);
        Assert.Equal("Width and height must be positive", problem.Detail);
    }

    [Fact]
    public void Wrapped_request_too_large_keeps_its_title()
    {
        var wrapped = new JsonException("JSON parse error", new IOException("read failed", new RequestTooLargeException(2_000_000)));

        var problem = ApiProblems.From(wrapped)!;

        Assert.Equal(400, problem.Status);
        Assert.Equal("Request too large", problem.Title);
        Assert.Contains("2000000", problem.Detail);
    }

    [Fact]
    public void No_conclusion_is_422_and_reports_how_far_the_walk_got()
    {
        var problem = ApiProblems.From(new NoConclusionException(1))!;

        Assert.Equal(422, problem.Status);
        Assert.Equal("No conclusion", problem.Title);
        Assert.Equal(1, problem.Extensions["generationsAttempted"]);
    }

    [Fact]
    public void Field_validation_lists_each_field_without_a_trailing_separator()
    {
        var problem = ApiProblems.From(new RequestValidationException(
        [
            new FieldError("width", "must be greater than or equal to 1"),
            new FieldError("height", "must be greater than or equal to 1"),
        ]))!;

        Assert.Equal(400, problem.Status);
        Assert.Equal("Validation failed", problem.Title);
        Assert.Equal(
            "width: must be greater than or equal to 1; height: must be greater than or equal to 1",
            problem.Detail);
    }

    [Fact]
    public void Validation_with_no_messages_still_explains_the_failure()
    {
        var problem = ApiProblems.From(new RequestValidationException([]))!;

        Assert.Equal("Request validation failed", problem.Detail);
    }

    [Fact]
    public void An_unexpected_exception_has_no_mapping_and_becomes_a_500()
    {
        Assert.Null(ApiProblems.From(new InvalidOperationException("cache is missing a row")));
    }
}
