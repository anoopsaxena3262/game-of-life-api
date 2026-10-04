namespace GameOfLife.Application;

/// <summary>A use-case result. The API maps this to a status code.</summary>
public abstract record Result<T>
{
    private Result()
    {
    }

    public sealed record Ok(T Value) : Result<T>;

    public sealed record Fail(AppError Error) : Result<T>;
}

/// <summary>A failure the API can turn into a problem document.</summary>
public sealed record AppError(string Title, int Status, string Type, string Detail);
