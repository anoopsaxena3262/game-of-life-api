using System.Text.Json;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace GameOfLife.Api.Http;

/// <summary>
/// Writes every problem document. An error is always <c>application/problem+json</c>, whatever
/// the client's Accept header: <c>type</c> is <c>about:blank</c> and <c>instance</c> is the
/// request path as the client sent it, unless the caller leaves it out.
/// </summary>
public static class ProblemWriter
{
    public const string ContentType = "application/problem+json";

    public static async Task WriteAsync(HttpContext context, ProblemDetails problem, bool includeInstance = true)
    {
        problem.Type = "about:blank";
        problem.Instance = includeInstance ? RequestPath(context) : null;
        context.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;

        // An Accept header that cannot be parsed leaves nothing the body could be written as.
        if (!JsonNegotiation.AcceptIsWellFormed(context.Request))
        {
            return;
        }

        context.Response.ContentType = ContentType;
        var json = context.RequestServices.GetRequiredService<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>().Value.SerializerOptions;
        await JsonSerializer.SerializeAsync(context.Response.Body, problem, json, context.RequestAborted);
    }

    // The path as the client sent it, still percent-encoded and without the query string.
    private static string? RequestPath(HttpContext context)
    {
        var raw = context.Features.Get<IHttpRequestFeature>()?.RawTarget;
        if (string.IsNullOrEmpty(raw) || raw[0] != '/')
        {
            return context.Request.Path.Value;
        }

        var query = raw.IndexOf('?');
        return query < 0 ? raw : raw[..query];
    }
}

/// <summary>
/// Which JSON media type a successful response is written as. A body can be
/// <c>application/json</c> or any <c>application/*+json</c> type the client names.
/// </summary>
public static class JsonNegotiation
{
    public const string Acceptable = "Acceptable representations: [application/json, application/*+json].";

    /// <returns>The content type to write, or null when the Accept header rules out JSON.</returns>
    public static string? SelectContentType(HttpRequest request)
    {
        var accept = request.Headers.Accept;
        if (accept.Count == 0 || string.IsNullOrWhiteSpace(accept.ToString()))
        {
            return "application/json";
        }

        if (!MediaTypeHeaderValue.TryParseList(accept, out var ranges))
        {
            return null;
        }

        // Most specific first, then by quality; the first range JSON can satisfy decides.
        // A quality of 0 is not treated as a refusal.
        var ordered = ranges
            .Select((range, index) => (range, index))
            .OrderBy(item => Specificity(item.range))
            .ThenByDescending(item => item.range.Quality ?? 1.0)
            .ThenBy(item => item.index)
            .Select(item => item.range);

        foreach (var range in ordered)
        {
            var type = range.Type.Value;
            var subtype = range.SubType.Value;
            if (type == "*" || (type is not null && type.Equals("application", StringComparison.OrdinalIgnoreCase)
                    && (subtype == "*" || string.Equals(subtype, "json", StringComparison.OrdinalIgnoreCase))))
            {
                return "application/json";
            }

            if (type is not null && type.Equals("application", StringComparison.OrdinalIgnoreCase)
                && subtype is not null && subtype.EndsWith("+json", StringComparison.OrdinalIgnoreCase))
            {
                return $"{type}/{subtype}";
            }
        }

        return null;
    }

    public static bool AcceptIsWellFormed(HttpRequest request)
    {
        var accept = request.Headers.Accept;
        return accept.Count == 0 || string.IsNullOrWhiteSpace(accept.ToString())
            || MediaTypeHeaderValue.TryParseList(accept, out _);
    }

    // 0 for type/subtype, 1 for type/*, 2 for */*.
    private static int Specificity(MediaTypeHeaderValue range) =>
        range.MatchesAllTypes ? 2 : range.MatchesAllSubTypes ? 1 : 0;

    /// <summary>
    /// A request Content-Type the way the reference service reports it: <c>type/subtype</c> and
    /// each parameter as <c>;name=value</c>, with <c>charset=UTF-8</c> added when the client sent
    /// none, and <c>application/octet-stream</c> when there is no header at all.
    /// </summary>
    public static string DescribeContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return "application/octet-stream";
        }

        if (!MediaTypeHeaderValue.TryParse(contentType, out var parsed))
        {
            return contentType;
        }

        var parameters = parsed.Parameters.Select(p => $";{p.Name.Value}={p.Value.Value}").ToList();
        if (parsed.Charset.Value is null)
        {
            parameters.Add(";charset=UTF-8");
        }

        return $"{parsed.MediaType.Value}{string.Concat(parameters)}";
    }
}
