using GameOfLife.Api.Http;

namespace GameOfLife.Api.Problems;

/// <summary>
/// The body was larger than <c>GameOfLife:MaxRequestBytes</c>. Thrown while the body is
/// being read, so the grid is never built.
/// </summary>
public sealed class RequestTooLargeException(long maxBytes) : Exception($"Request body exceeds {maxBytes} bytes");

/// <summary>A request the endpoint cannot serve: bad JSON, a bad path or query value, an unsupported or unacceptable media type.</summary>
public sealed class BadRequestException(int status, string title, string detail) : Exception(detail)
{
    public int Status { get; } = status;

    public string Title { get; } = title;

    /// <summary>The body is missing, is not JSON, or does not match the request shape.</summary>
    public static BadRequestException UnreadableBody() => new(StatusCodes.Status400BadRequest, "Bad Request", "Failed to read request");

    /// <summary>A path or query value that does not convert to its type.</summary>
    public static BadRequestException BadValue(string name, string value) =>
        new(StatusCodes.Status400BadRequest, "Bad Request", $"Failed to convert '{name}' with value: '{value}'");

    /// <summary>The Accept header rules out every JSON media type the response could be written as.</summary>
    public static BadRequestException NotAcceptable() =>
        new(StatusCodes.Status406NotAcceptable, "Not Acceptable", JsonNegotiation.Acceptable);

    public static BadRequestException UnsupportedMediaType(string? contentType) =>
        new(
            StatusCodes.Status415UnsupportedMediaType,
            "Unsupported Media Type",
            $"Content-Type '{JsonNegotiation.DescribeContentType(contentType)}' is not supported.");
}

/// <summary>The upload JSON parsed but broke a field rule. Each entry is <c>field: message</c>.</summary>
public sealed class RequestValidationException(IReadOnlyList<FieldError> errors)
    : Exception(ApiProblems.ValidationDetail(errors))
{
    public IReadOnlyList<FieldError> Errors { get; } = errors;
}

public sealed record FieldError(string Field, string Message);
