using Microsoft.AspNetCore.Diagnostics;

namespace GameOfLife.Api.Endpoints;

/// <summary>Scaffold handlers throw until a phase implements them. Callers get 501, not an empty 500.</summary>
public sealed class NotImplementedExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not NotImplementedException)
        {
            return false;
        }

        httpContext.Response.StatusCode = StatusCodes.Status501NotImplemented;
        await Results.Problem(
            title: "Not implemented",
            detail: "This endpoint is scaffolded and has no behavior yet.",
            statusCode: StatusCodes.Status501NotImplemented,
            type: "https://game-of-life.dev/problems/not-implemented")
            .ExecuteAsync(httpContext);
        return true;
    }
}
