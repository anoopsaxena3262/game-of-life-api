using System.Globalization;
using System.Text.Json;
using GameOfLife.Api.Problems;
using GameOfLife.Application;
using GameOfLife.Domain;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;

namespace GameOfLife.Api.Endpoints;

/// <summary>
/// The board endpoints. Every generation endpoint is a GET and none of them change the
/// stored board. /next is the same read as /generations/1; it is a separate route
/// because it is its own capability, and it is logged as that call.
/// </summary>
/// <remarks>
/// Path, query and body values are parsed here rather than bound by the framework, so a
/// value that does not convert is a 400 problem document, the same as every other error.
/// </remarks>
public static class BoardEndpoints
{
    private const string LoggerName = "GameOfLife.Api.Endpoints.BoardEndpoints";

    public static IEndpointRouteBuilder MapBoardEndpoints(this IEndpointRouteBuilder app)
    {
        // Authorization is intentionally not required. The call would go on this group.
        var group = app.MapGroup("/api/v1/boards");

        group.MapPost("", CreateBoard);
        group.MapGet("/{id}", GetBoard);
        group.MapGet("/{id}/next", GetNext);
        group.MapGet("/{id}/generations/{n}", GetGeneration);
        group.MapGet("/{id}/final", GetFinal);

        return app;
    }

    /// <summary>Upload a board. 201 with a Location header.</summary>
    private static async Task<IResult> CreateBoard(
        HttpRequest request,
        BoardService service,
        IOptions<JsonOptions> json,
        ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        var body = await ReadCreateRequestAsync(request, json.Value.SerializerOptions, cancellationToken);
        loggers.CreateLogger(LoggerName).LogInformation(
            "POST /boards width={Width} height={Height}", body.Width, body.Height);

        var id = await service.CreateAsync(body.Width, body.Height, body.Cells, cancellationToken);
        var board = await service.GetAsync(id, cancellationToken);
        var response = new BoardResponse(id, board.Width, board.Height, 0, Cells(board.InitialState, board));
        return TypedResults.Created($"/api/v1/boards/{id}", response);
    }

    /// <summary>Board metadata and generation 0.</summary>
    private static async Task<IResult> GetBoard(
        string id, BoardService service, ILoggerFactory loggers, CancellationToken cancellationToken)
    {
        var boardId = ParseId(id);
        loggers.CreateLogger(LoggerName).LogInformation("GET /boards/{Id}", boardId);

        var board = await service.GetAsync(boardId, cancellationToken);
        return TypedResults.Ok(new BoardResponse(boardId, board.Width, board.Height, 0, Cells(board.InitialState, board)));
    }

    /// <summary>One generation forward. Same read as /generations/1, logged as its own call.</summary>
    private static async Task<IResult> GetNext(
        string id, BoardService service, ILoggerFactory loggers, CancellationToken cancellationToken)
    {
        var boardId = ParseId(id);
        loggers.CreateLogger(LoggerName).LogInformation("GET /boards/{Id}/next", boardId);
        return await GenerationAsync(boardId, 1, service, cancellationToken);
    }

    /// <summary>The state n generations after upload.</summary>
    private static async Task<IResult> GetGeneration(
        string id, string n, BoardService service, ILoggerFactory loggers, CancellationToken cancellationToken)
    {
        var boardId = ParseId(id);
        var index = ParseInt("n", n);
        loggers.CreateLogger(LoggerName).LogInformation("GET /boards/{Id}/generations/{N}", boardId, index);
        return await GenerationAsync(boardId, index, service, cancellationToken);
    }

    /// <summary>Final state, or 422 if the board does not conclude within the limit.</summary>
    private static async Task<IResult> GetFinal(
        string id,
        string? maxGenerations,
        BoardService service,
        ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        var boardId = ParseId(id);
        int? requestedMax = string.IsNullOrEmpty(maxGenerations) ? null : ParseInt("maxGenerations", maxGenerations);
        loggers.CreateLogger(LoggerName).LogInformation(
            "GET /boards/{Id}/final maxGenerations={MaxGenerations}", boardId, requestedMax);

        var board = await service.GetAsync(boardId, cancellationToken);
        var outcome = await service.FinalStateAsync(boardId, requestedMax, cancellationToken);
        var result = outcome.Result;
        return TypedResults.Ok(new FinalStateResponse(
            boardId,
            board.Width,
            board.Height,
            Cells(result.State, board),
            result.Kind,
            result.FirstOccurrence,
            result.Period,
            result.GenerationsComputed,
            outcome.GenerationsLimit));
    }

    private static async Task<IResult> GenerationAsync(
        Guid id, int index, BoardService service, CancellationToken cancellationToken)
    {
        var board = await service.GetAsync(id, cancellationToken);
        var state = await service.GenerationAtAsync(id, index, cancellationToken);
        return TypedResults.Ok(new GenerationResponse(id, board.Width, board.Height, index, Cells(state, board)));
    }

    private static async Task<CreateBoardRequest> ReadCreateRequestAsync(
        HttpRequest request, JsonSerializerOptions json, CancellationToken cancellationToken)
    {
        if (!request.HasJsonContentType())
        {
            throw BadRequestException.UnsupportedMediaType(request.ContentType);
        }

        CreateBoardRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<CreateBoardRequest>(request.Body, json, cancellationToken);
        }
        catch (JsonException)
        {
            // Not JSON, or a value of the wrong type, including a null cell. A null
            // cell must not be stored as dead.
            throw BadRequestException.UnreadableBody();
        }

        if (body is null)
        {
            throw BadRequestException.UnreadableBody();
        }

        var errors = new List<FieldError>();
        if (body.Width < 1)
        {
            errors.Add(new FieldError("width", "must be greater than or equal to 1"));
        }

        if (body.Height < 1)
        {
            errors.Add(new FieldError("height", "must be greater than or equal to 1"));
        }

        if (body.Cells is null)
        {
            errors.Add(new FieldError("cells", "must not be null"));
        }

        return errors.Count == 0 ? body : throw new RequestValidationException(errors);
    }

    // Canonical 8-4-4-4-12 form only, in either case.
    private static Guid ParseId(string value) =>
        Guid.TryParseExact(value, "D", out var id) ? id : throw BadRequestException.BadValue("id", value);

    private static int ParseInt(string name, string value) =>
        int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw BadRequestException.BadValue(name, value);

    private static bool[][] Cells(string state, Board board) => StateCodec.Deserialize(state, board.Width, board.Height);
}
