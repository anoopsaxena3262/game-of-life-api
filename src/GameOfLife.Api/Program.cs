using System.Text.Json;
using System.Text.Json.Serialization;
using GameOfLife.Api.Endpoints;
using GameOfLife.Api.Problems;
using GameOfLife.Application;
using GameOfLife.Infrastructure;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// One line per entry on the console. Levels come from the Logging:LogLevel section.
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options =>
{
    options.SingleLine = true;
    options.TimestampFormat = "HH:mm:ss ";
});

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

var app = builder.Build();

// Reading the value validates it, so a contradictory configuration stops startup here.
var limits = app.Services.GetRequiredService<IOptions<GameOfLifeOptions>>().Value;
app.Logger.LogInformation(
    "limits maxGenerations={MaxGenerations} ceiling={Ceiling} maxCells={MaxCells} maxCellGenerations={MaxCellGenerations} maxRequestBytes={MaxRequestBytes}",
    limits.MaxGenerations, limits.MaxGenerationsCeiling, limits.MaxCells, limits.MaxCellGenerations, limits.MaxRequestBytes);

await app.Services.InitializeDatabaseAsync();

app.UseExceptionHandler();
// An unknown route or a wrong method gets the same problem document as every other error.
app.UseStatusCodePages();
app.UseMiddleware<RequestSizeLimitMiddleware>();

app.MapBoardEndpoints();

app.Run();

public partial class Program;
