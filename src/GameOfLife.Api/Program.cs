using GameOfLife.Api.Endpoints;
using GameOfLife.Application;
using GameOfLife.Infrastructure;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) =>
    configuration
        .MinimumLevel.Information()
        .Enrich.FromLogContext()
        .WriteTo.Console());

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<NotImplementedExceptionHandler>();
builder.Services.Configure<GameOfLifeOptions>(builder.Configuration.GetSection(GameOfLifeOptions.SectionName));
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

await app.Services.InitializeDatabaseAsync();

app.UseExceptionHandler();
app.UseSerilogRequestLogging();

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
