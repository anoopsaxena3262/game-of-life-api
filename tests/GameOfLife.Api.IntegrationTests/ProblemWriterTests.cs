using System.Text.Json;
using GameOfLife.Api.Http;
using GameOfLife.Api.Problems;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;

namespace GameOfLife.Api.IntegrationTests;

/// <summary>
/// The problem document itself, written directly: the in-memory test server does not keep
/// the raw request target the way the real server does.
/// </summary>
public sealed class ProblemWriterTests
{
    [Fact]
    public async Task The_instance_is_the_path_as_sent_without_the_query()
    {
        var context = NewContext(rawTarget: "/api/v1/boards/%7Babc%7D?x=1", decodedPath: "/api/v1/boards/{abc}", accept: null);

        var body = await WriteAsync(context);

        Assert.Equal("/api/v1/boards/%7Babc%7D", body.GetProperty("instance").GetString());
        Assert.Equal("about:blank", body.GetProperty("type").GetString());
    }

    [Fact]
    public async Task Without_a_raw_target_the_instance_is_the_path()
    {
        var context = NewContext(rawTarget: null, decodedPath: "/api/v1/boards/x", accept: null);

        var body = await WriteAsync(context);

        Assert.Equal("/api/v1/boards/x", body.GetProperty("instance").GetString());
    }

    [Fact]
    public async Task An_error_is_written_as_problem_json_whatever_the_accept_header()
    {
        var context = NewContext(rawTarget: "/api/v1/boards/x", decodedPath: "/api/v1/boards/x", accept: "application/xml");

        var body = await WriteAsync(context);

        Assert.Equal("application/problem+json", context.Response.ContentType);
        Assert.Equal("Bad Request", body.GetProperty("title").GetString());
    }

    private static async Task<JsonElement> WriteAsync(HttpContext context)
    {
        await ProblemWriter.WriteAsync(context, ApiProblems.Problem(400, "Bad Request", "detail"));
        context.Response.Body.Position = 0;
        return JsonDocument.Parse(context.Response.Body).RootElement;
    }

    private static DefaultHttpContext NewContext(string? rawTarget, string decodedPath, string? accept)
    {
        var context = new DefaultHttpContext { RequestServices = new ServiceCollection().AddOptions().BuildServiceProvider() };
        context.Request.Path = decodedPath;
        if (rawTarget is not null)
        {
            context.Features.Get<IHttpRequestFeature>()!.RawTarget = rawTarget;
        }

        if (accept is not null)
        {
            context.Request.Headers.Accept = accept;
        }

        context.Response.Body = new MemoryStream();
        return context;
    }
}
