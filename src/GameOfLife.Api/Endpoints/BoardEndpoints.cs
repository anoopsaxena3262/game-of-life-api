using GameOfLife.Application;
using Microsoft.AspNetCore.Mvc;

namespace GameOfLife.Api.Endpoints;

public static class BoardEndpoints
{
    public static IEndpointRouteBuilder MapBoardEndpoints(this IEndpointRouteBuilder app)
    {
        // Authorization is intentionally not required. The call would go on this group.
        var group = app.MapGroup("/api/v1");

        group.MapPost("/boards", CreateBoard);
        group.MapGet("/boards/{id:guid}", GetBoard);
        group.MapGet("/boards/{id:guid}/next", GetNext);
        group.MapGet("/boards/{id:guid}/generations/{n:int}", GetGeneration);
        group.MapGet("/boards/{id:guid}/final", GetFinal);

        return app;
    }

    private static async Task<IResult> CreateBoard(
        CreateBoardCommand command,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        ICreateBoard handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(command with { IdempotencyKey = idempotencyKey }, cancellationToken);
        return result.ToHttp(created => Results.Created($"/api/v1/boards/{created.Id}", created));
    }

    private static async Task<IResult> GetBoard(
        Guid id,
        IGetBoard handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(id, cancellationToken);
        return result.ToHttp(Results.Ok);
    }

    private static async Task<IResult> GetNext(
        Guid id,
        IGetGeneration handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(id, generation: 1, cancellationToken);
        return result.ToHttp(Results.Ok);
    }

    private static async Task<IResult> GetGeneration(
        Guid id,
        int n,
        IGetGeneration handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(id, n, cancellationToken);
        return result.ToHttp(Results.Ok);
    }

    private static async Task<IResult> GetFinal(
        Guid id,
        int? maxGenerations,
        IGetFinalState handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(id, maxGenerations, cancellationToken);
        return result.ToHttp(Results.Ok);
    }
}
