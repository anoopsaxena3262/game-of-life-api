using GameOfLife.Application;

namespace GameOfLife.Api.Endpoints;

public static class ResultHttp
{
    public static IResult ToHttp<T>(this Result<T> result, Func<T, IResult> onSuccess)
    {
        return result switch
        {
            Result<T>.Ok ok => onSuccess(ok.Value),
            Result<T>.Fail fail => Results.Problem(
                title: fail.Error.Title,
                detail: fail.Error.Detail,
                statusCode: fail.Error.Status,
                type: fail.Error.Type),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError)
        };
    }
}
