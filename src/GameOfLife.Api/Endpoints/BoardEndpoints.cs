using System.Text.Json;
using GameOfLife.Api.Http;
using GameOfLife.Api.Problems;
using GameOfLife.Application;
using GameOfLife.Domain;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace GameOfLife.Api.Endpoints;

/// <summary>
/// The board endpoints. Every generation endpoint is a GET and none of them change the
/// stored board. /next is the same read as /generations/1; it is a separate route
/// because it is its own capability, and it is logged as that call.
/// </summary>
/// <remarks>
/// Path, query and body values are parsed here rather than bound by the framework, with the
/// conversion rules in <see cref="SpringConversions"/> and the JSON rules in
/// <see cref="LenientBooleanConverter"/> and <see cref="LenientInt32Converter"/>. A value
/// that does not convert is a 400 problem document, the same as every other error.
/// </remarks>
public static class BoardEndpoints
{
    private const string LoggerName = "GameOfLife.Api.Endpoints.BoardEndpoints";

    private static readonly string[] GetAndHead = [HttpMethods.Get, HttpMethods.Head];

    // Request bodies only: lenient booleans and ints, names matched exactly.
    private static readonly JsonSerializerOptions RequestJson = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        Converters = { new LenientBooleanConverter(), new LenientInt32Converter() },
    };

    public static IEndpointRouteBuilder MapBoardEndpoints(this IEndpointRouteBuilder app)
    {
        // Authorization is intentionally not required. The call would go on this group.
        var group = app.MapGroup("/api/v1/boards");

        group.MapPost("", CreateBoard);
        group.MapMethods("/{id}", GetAndHead, GetBoard);
        group.MapMethods("/{id}/next", GetAndHead, GetNext);
        group.MapMethods("/{id}/generations/{n}", GetAndHead, GetGeneration);
        group.MapMethods("/{id}/final", GetAndHead, GetFinal);

        // OPTIONS answers with the methods each route supports and no body.
        MapOptions(group, "", "POST,OPTIONS");
        foreach (var pattern in new[] { "/{id}", "/{id}/next", "/{id}/generations/{n}", "/{id}/final" })
        {
            MapOptions(group, pattern, "GET,HEAD,OPTIONS");
        }

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
        var body = await ReadCreateRequestAsync(request, cancellationToken);
        loggers.CreateLogger(LoggerName).LogInformation(
            "POST /boards width={Width} height={Height}", body.Width, body.Height);

        var id = await service.CreateAsync(body.Width!.Value, body.Height!.Value, body.Cells, cancellationToken);
        var board = await service.GetAsync(id, cancellationToken);
        var response = new BoardResponse(id, board.Width, board.Height, 0, Cells(board.InitialState, board));

        // The board is stored before the response type is chosen, so an unacceptable Accept
        // header still creates it.
        request.HttpContext.Response.Headers.Location = $"/api/v1/boards/{id}";
        return Json(request, response, json, StatusCodes.Status201Created);
    }

    /// <summary>Board metadata and generation 0.</summary>
    private static async Task<IResult> GetBoard(
        string id, HttpRequest request, BoardService service, IOptions<JsonOptions> json, ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        var boardId = ParseId(id);
        loggers.CreateLogger(LoggerName).LogInformation("GET /boards/{Id}", boardId);

        var board = await service.GetAsync(boardId, cancellationToken);
        return Json(request, new BoardResponse(boardId, board.Width, board.Height, 0, Cells(board.InitialState, board)), json);
    }

    /// <summary>One generation forward. Same read as /generations/1, logged as its own call.</summary>
    private static async Task<IResult> GetNext(
        string id, HttpRequest request, BoardService service, IOptions<JsonOptions> json, ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        var boardId = ParseId(id);
        loggers.CreateLogger(LoggerName).LogInformation("GET /boards/{Id}/next", boardId);
        return await GenerationAsync(boardId, 1, request, service, json, cancellationToken);
    }

    /// <summary>The state n generations after upload.</summary>
    private static async Task<IResult> GetGeneration(
        string id, string n, HttpRequest request, BoardService service, IOptions<JsonOptions> json,
        ILoggerFactory loggers, CancellationToken cancellationToken)
    {
        var boardId = ParseId(id);
        var index = ParseInt("n", n) ?? throw BadRequestException.BadValue("n", n);
        loggers.CreateLogger(LoggerName).LogInformation("GET /boards/{Id}/generations/{N}", boardId, index);
        return await GenerationAsync(boardId, index, request, service, json, cancellationToken);
    }

    /// <summary>Final state, or 422 if the board does not conclude within the limit.</summary>
    private static async Task<IResult> GetFinal(
        string id, HttpRequest request, BoardService service, IOptions<JsonOptions> json, ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        var boardId = ParseId(id);
        var rawMax = FirstQueryValue(request, "maxGenerations");
        int? requestedMax = rawMax is null ? null : ParseInt("maxGenerations", rawMax);
        loggers.CreateLogger(LoggerName).LogInformation(
            "GET /boards/{Id}/final maxGenerations={MaxGenerations}", boardId, requestedMax);

        var board = await service.GetAsync(boardId, cancellationToken);
        var outcome = await service.FinalStateAsync(boardId, requestedMax, cancellationToken);
        var result = outcome.Result;
        return Json(request, new FinalStateResponse(
            boardId,
            board.Width,
            board.Height,
            Cells(result.State, board),
            result.Kind,
            result.FirstOccurrence,
            result.Period,
            result.GenerationsComputed,
            outcome.GenerationsLimit), json);
    }

    private static async Task<IResult> GenerationAsync(
        Guid id, int index, HttpRequest request, BoardService service, IOptions<JsonOptions> json,
        CancellationToken cancellationToken)
    {
        var board = await service.GetAsync(id, cancellationToken);
        var state = await service.GenerationAtAsync(id, index, cancellationToken);
        return Json(request, new GenerationResponse(id, board.Width, board.Height, index, Cells(state, board)), json);
    }

    /// <summary>A successful body, written as the JSON type the Accept header allows; 406 if it allows none.</summary>
    private static IResult Json<T>(HttpRequest request, T value, IOptions<JsonOptions> json, int status = StatusCodes.Status200OK)
    {
        var contentType = JsonNegotiation.SelectContentType(request) ?? throw BadRequestException.NotAcceptable();
        return Results.Json(value, json.Value.SerializerOptions, contentType, status);
    }

    private static async Task<CreateBoardRequest> ReadCreateRequestAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (!request.HasJsonContentType())
        {
            throw BadRequestException.UnsupportedMediaType(request.ContentType);
        }

        // The whole body is read (the size limit still applies), then one JSON value is taken
        // from the front of it. Anything after that value is ignored.
        using var buffer = new MemoryStream();
        await request.Body.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.GetBuffer().AsSpan(0, (int)buffer.Length);
        if (bytes.StartsWith("﻿"u8))
        {
            bytes = bytes[3..];
        }

        CreateBoardRequest? body;
        try
        {
            var reader = new Utf8JsonReader(bytes);
            body = JsonSerializer.Deserialize<CreateBoardRequest>(ref reader, RequestJson);
        }
        catch (JsonException)
        {
            // Not JSON, or a value of the wrong type, including a null cell. A null
            // cell must not be stored as dead.
            throw BadRequestException.UnreadableBody();
        }

        // A missing or null width or height cannot become an int: the body is unreadable.
        if (body?.Width is null || body.Height is null)
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

    private static void MapOptions(RouteGroupBuilder group, string pattern, string allow) =>
        group.MapMethods(pattern, [HttpMethods.Options], (HttpResponse response) =>
        {
            response.Headers.Allow = allow;
            return Results.Ok();
        });

    // The first value of a query parameter whose name matches exactly, decoded ('+' is a space).
    private static string? FirstQueryValue(HttpRequest request, string name)
    {
        foreach (var pair in new QueryStringEnumerable(request.QueryString.Value))
        {
            if (pair.DecodeName().Span.SequenceEqual(name))
            {
                return pair.DecodeValue().ToString();
            }
        }

        return null;
    }

    private static Guid ParseId(string value)
    {
        try
        {
            return SpringConversions.ParseUuid(value);
        }
        catch (FormatException)
        {
            throw BadRequestException.BadValue("id", value);
        }
    }

    private static int? ParseInt(string name, string value)
    {
        try
        {
            return SpringConversions.ParseInt(value);
        }
        catch (FormatException)
        {
            throw BadRequestException.BadValue(name, value);
        }
    }

    private static bool[][] Cells(string state, Board board) => StateCodec.Deserialize(state, board.Width, board.Height);
}
