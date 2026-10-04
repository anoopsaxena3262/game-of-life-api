using GameOfLife.Application;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace GameOfLife.Api.Problems;

/// <summary>Writes a problem document for every exception that reaches the pipeline.</summary>
public sealed class ApiExceptionHandler(IProblemDetailsService problemDetails, ILogger<ApiExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var problem = ApiProblems.From(exception);
        if (problem is null)
        {
            logger.LogError(exception, "unhandled exception");
            problem = ApiProblems.Problem(StatusCodes.Status500InternalServerError, "Internal Server Error", null);
        }
        else
        {
            Log(exception, problem);
        }

        httpContext.Response.StatusCode = problem.Status!.Value;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
        });
    }

    private void Log(Exception exception, ProblemDetails problem)
    {
        switch (exception)
        {
            case NoConclusionException noConclusion:
                // A documented outcome. Logged once, at Information.
                logger.LogInformation("no conclusion after {Generations} generations", noConclusion.GenerationsAttempted);
                break;
            case BoardNotFoundException:
                logger.LogWarning("board not found: {Detail}", problem.Detail);
                break;
            default:
                logger.LogWarning("{Title}: {Detail}", problem.Title, problem.Detail);
                break;
        }
    }
}
