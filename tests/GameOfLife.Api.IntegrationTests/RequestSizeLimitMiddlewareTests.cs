using System.Text.Json;
using GameOfLife.Api.Problems;
using GameOfLife.Application;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GameOfLife.Api.IntegrationTests;

/// <summary>
/// The declared-length check, run directly: the in-memory test server does not expose the
/// client's Content-Length to the middleware.
/// </summary>
public sealed class RequestSizeLimitMiddlewareTests
{
    [Fact]
    public async Task A_declared_length_over_the_cap_is_rejected_without_reading_the_body_and_without_an_instance()
    {
        var context = NewContext(contentLength: 101);
        var nextRan = false;
        var middleware = Middleware(maxBytes: 100, _ => { nextRan = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(context);

        Assert.False(nextRan);
        Assert.Equal(400, context.Response.StatusCode);
        Assert.Equal("application/problem+json", context.Response.ContentType);
        context.Response.Body.Position = 0;
        var body = JsonDocument.Parse(context.Response.Body).RootElement;
        Assert.Equal("Request too large", body.GetProperty("title").GetString());
        Assert.Equal("Request body exceeds 100 bytes", body.GetProperty("detail").GetString());
        Assert.False(body.TryGetProperty("instance", out _));
    }

    [Fact]
    public async Task A_declared_length_at_the_cap_passes_through()
    {
        var context = NewContext(contentLength: 100);
        var nextRan = false;
        var middleware = Middleware(maxBytes: 100, _ => { nextRan = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(context);

        Assert.True(nextRan);
    }

    private static RequestSizeLimitMiddleware Middleware(int maxBytes, RequestDelegate next) =>
        new(next, Options.Create(new GameOfLifeOptions { MaxRequestBytes = maxBytes }), NullLogger<RequestSizeLimitMiddleware>.Instance);

    private static DefaultHttpContext NewContext(long contentLength)
    {
        var services = new ServiceCollection().AddOptions().BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/api/v1/boards";
        context.Request.ContentLength = contentLength;
        context.Response.Body = new MemoryStream();
        return context;
    }
}
