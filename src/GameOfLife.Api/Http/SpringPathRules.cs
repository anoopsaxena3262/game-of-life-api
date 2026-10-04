namespace GameOfLife.Api.Http;

/// <summary>
/// Path matching the way the reference service does it, applied before routing:
/// <list type="bullet">
/// <item><c>;name=value</c> parameters inside a path segment are removed.</item>
/// <item>A path that ends in <c>/</c> matches nothing (404).</item>
/// <item>Route literals are case-sensitive: <c>/API/v1/boards</c> matches nothing (404).</item>
/// <item>The Allow header on a 405 lists the mapped methods only, without HEAD or OPTIONS,
/// separated by commas without spaces.</item>
/// </list>
/// </summary>
public sealed class SpringPathRules(RequestDelegate next)
{
    // The literal segments of every board route, by position: /api/v1/boards/{id}/{action}/{n}.
    private static readonly string?[] Literals = ["api", "v1", "boards", null, null, null];
    private static readonly string[] Actions = ["next", "generations", "final"];

    public Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "/";
        if (path.Contains(';'))
        {
            path = string.Join('/', path.Split('/').Select(segment => segment.Split(';')[0]));
            context.Request.Path = path;
        }

        if ((path.Length > 1 && path.EndsWith('/')) || LiteralCaseDiffers(path))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return Task.CompletedTask;
        }

        context.Response.OnStarting(() =>
        {
            if (context.Response.StatusCode == StatusCodes.Status405MethodNotAllowed
                && context.Response.Headers.Allow.Count > 0)
            {
                var methods = context.Response.Headers.Allow.ToString()
                    .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                    .Where(method => method is not ("HEAD" or "OPTIONS"));
                context.Response.Headers.Allow = string.Join(',', methods);
            }

            return Task.CompletedTask;
        });

        return next(context);
    }

    // True when a segment spells a route literal in a different case.
    private static bool LiteralCaseDiffers(string path)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < segments.Length && i < Literals.Length; i++)
        {
            var expected = i == 4 ? Actions.FirstOrDefault(a => a.Equals(segments[i], StringComparison.OrdinalIgnoreCase)) : Literals[i];
            if (expected is not null
                && segments[i].Equals(expected, StringComparison.OrdinalIgnoreCase)
                && !segments[i].Equals(expected, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
