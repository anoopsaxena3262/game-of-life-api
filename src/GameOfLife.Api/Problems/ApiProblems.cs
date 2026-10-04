using GameOfLife.Application;
using Microsoft.AspNetCore.Mvc;

namespace GameOfLife.Api.Problems;

/// <summary>
/// Maps exceptions to RFC 7807 problem documents. Kept free of HTTP plumbing so each
/// mapping is testable on its own.
/// </summary>
public static class ApiProblems
{
    /// <returns>The problem for a known exception, or null for an unexpected one.</returns>
    public static ProblemDetails? From(Exception exception)
    {
        // A size-limit failure can arrive wrapped, for example inside a JSON or I/O
        // exception raised while the body was being read. It is still "Request too large".
        if (FindCause<RequestTooLargeException>(exception) is { } tooLarge)
        {
            return Problem(StatusCodes.Status400BadRequest, "Request too large", tooLarge.Message);
        }

        switch (exception)
        {
            case BoardNotFoundException notFound:
                // 404: the id is well formed, but no board is stored under it.
                return Problem(StatusCodes.Status404NotFound, "Board not found", notFound.Message);
            case InvalidBoardException invalid:
                // 400: the board itself is malformed or over a limit. The request is the problem.
                return Problem(StatusCodes.Status400BadRequest, "Invalid board", invalid.Message);
            case NoConclusionException noConclusion:
                // 422: the walk used its allowed generations and did not repeat. That is a
                // documented outcome, not a server fault, so it stays a client error.
                var problem = Problem(StatusCodes.Status422UnprocessableEntity, "No conclusion", noConclusion.Message);
                problem.Extensions["generationsAttempted"] = noConclusion.GenerationsAttempted;
                return problem;
            case RequestValidationException validation:
                return Problem(StatusCodes.Status400BadRequest, "Validation failed", ValidationDetail(validation.Errors));
            case BadRequestException badRequest:
                return Problem(badRequest.Status, badRequest.Title, badRequest.Message);
            case BadHttpRequestException badHttpRequest:
                // Raised by the server while reading the request, for example a truncated body.
                return Problem(badHttpRequest.StatusCode, "Bad Request", "Failed to read request");
            default:
                return null;
        }
    }

    /// <summary>One <c>field: message</c> per error, separated by "; ".</summary>
    public static string ValidationDetail(IReadOnlyList<FieldError> errors) =>
        errors.Count == 0
            ? "Request validation failed"
            : string.Join("; ", errors.Select(error => $"{error.Field}: {error.Message}"));

    public static ProblemDetails Problem(int status, string title, string? detail) =>
        new() { Status = status, Title = title, Detail = detail };

    private static T? FindCause<T>(Exception? exception)
        where T : Exception
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is T match)
            {
                return match;
            }
        }

        return null;
    }
}
