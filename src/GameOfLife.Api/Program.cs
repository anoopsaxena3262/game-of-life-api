using System.Text.Json;
using System.Text.Json.Serialization;
using GameOfLife.Api.Endpoints;
using GameOfLife.Api.Problems;
using GameOfLife.Application;
using GameOfLife.Infrastructure;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) =>
    configuration
        .MinimumLevel.Information()
        // The service's own loggers. Debug adds cache hits, misses and resume indexes.
        .MinimumLevel.Override(
            "GameOfLife",
            context.Configuration.GetValue("Serilog:MinimumLevel:Override:GameOfLife", LogEventLevel.Information))
        .Enrich.FromLogContext()
        .WriteTo.Console());

builder.Services.ConfigureHttpJsonOptions(options =>
{
    // camelCase names; terminationKind as EXTINCT, FIXED_POINT or CYCLE.
    options.SerializerOptions.PropertyNameCaseInsensitive = false;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper));
});
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
{
    // RFC 7807 with no problem-type URI: type, title, status, detail and the request path.
    context.ProblemDetails.Type = "about:blank";
    context.ProblemDetails.Instance = context.HttpContext.Request.Path;
    context.ProblemDetails.Extensions.Remove("traceId");
});
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddOptions<GameOfLifeOptions>()
    .Bind(builder.Configuration.GetSection(GameOfLifeOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddOpenApi();

var telemetry = builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation());

if (!string.IsNullOrWhiteSpace(builder.Configuration["OpenTelemetry:OtlpEndpoint"]))
{
    telemetry.WithTracing(tracing => tracing.AddOtlpExporter());
    telemetry.WithMetrics(metrics => metrics.AddOtlpExporter());
}
else if (builder.Configuration.GetValue("OpenTelemetry:ConsoleExporter", false))
{
    telemetry.WithTracing(tracing => tracing.AddConsoleExporter());
    telemetry.WithMetrics(metrics => metrics.AddConsoleExporter());
}

var app = builder.Build();

// Reading the value validates it, so a contradictory configuration stops startup here.
var limits = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<GameOfLifeOptions>>().Value;
app.Logger.LogInformation(
    "limits maxGenerations={MaxGenerations} ceiling={Ceiling} maxCells={MaxCells} maxCellGenerations={MaxCellGenerations} maxRequestBytes={MaxRequestBytes}",
    limits.MaxGenerations, limits.MaxGenerationsCeiling, limits.MaxCells, limits.MaxCellGenerations, limits.MaxRequestBytes);

await app.Services.InitializeDatabaseAsync();

app.UseExceptionHandler();
// An unknown route or a wrong method gets the same problem document as every other error.
app.UseStatusCodePages();
app.UseSerilogRequestLogging();
app.UseMiddleware<RequestSizeLimitMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false
});
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});
app.MapBoardEndpoints();

app.Run();

public partial class Program;
