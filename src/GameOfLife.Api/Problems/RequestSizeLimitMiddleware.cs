using GameOfLife.Application;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace GameOfLife.Api.Problems;

/// <summary>
/// Rejects a body before the grid is parsed. <c>MaxCells</c> runs only after the JSON
/// has already been turned into an array.
/// </summary>
public sealed class RequestSizeLimitMiddleware(
    RequestDelegate next,
    IOptions<GameOfLifeOptions> options,
    IProblemDetailsService problemDetails,
    ILogger<RequestSizeLimitMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        long max = options.Value.MaxRequestBytes;
        if (context.Request.ContentLength > max)
        {
            // An advertised length over the cap is answered here, without reading the body.
            var tooLarge = new RequestTooLargeException(max);
            logger.LogWarning("request too large: {Detail}", tooLarge.Message);
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await problemDetails.WriteAsync(new ProblemDetailsContext
            {
                HttpContext = context,
                ProblemDetails = ApiProblems.Problem(StatusCodes.Status400BadRequest, "Request too large", tooLarge.Message),
            });
            return;
        }

        // A chunked body, or one that lies about its length, is counted as it is read.
        context.Request.Body = new BoundedReadStream(context.Request.Body, max);
        await next(context);
    }

    private sealed class BoundedReadStream(Stream inner, long max) : Stream
    {
        private long _total;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => Count(inner.Read(buffer, offset, count));

        public override int Read(Span<byte> buffer) => Count(inner.Read(buffer));

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            Count(await inner.ReadAsync(buffer.AsMemory(offset, count), cancellationToken));

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            Count(await inner.ReadAsync(buffer, cancellationToken));

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        private int Count(int read)
        {
            _total += read;
            if (_total > max)
            {
                throw new RequestTooLargeException(max);
            }

            return read;
        }
    }
}
