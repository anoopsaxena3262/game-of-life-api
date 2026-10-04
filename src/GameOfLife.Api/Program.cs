using System.Text.Json;
using System.Text.Json.Serialization;
using GameOfLife.Api.Endpoints;
using GameOfLife.Api.Http;
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
// Problem documents are written by ProblemWriter; the exception handler middleware still
// requires the problem-details service to be registered.
builder.Services.AddProblemDetails();
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
app.UseStatusCodePages(context => ProblemWriter.WriteAsync(context.HttpContext, StatusProblem(context.HttpContext)));
app.UseMiddleware<SpringPathRules>();
app.UseRouting();
app.UseMiddleware<RequestSizeLimitMiddleware>();

app.MapBoardEndpoints();

app.Run();

// The problem for a status written without a body: no route, or a method the route lacks.
static Microsoft.AspNetCore.Mvc.ProblemDetails StatusProblem(HttpContext context)
{
    var status = context.Response.StatusCode;
    var title = Microsoft.AspNetCore.WebUtilities.ReasonPhrases.GetReasonPhrase(status);
    var detail = status switch
    {
        StatusCodes.Status404NotFound =>
            $"No static resource {System.Text.RegularExpressions.Regex.Replace(context.Request.Path.Value ?? "", "/{2,}", "/").Trim('/')}.",
        StatusCodes.Status405MethodNotAllowed => $"Method '{context.Request.Method}' is not supported.",
        _ => null,
    };
    return ApiProblems.Problem(status, title, detail);
}

public partial class Program;
