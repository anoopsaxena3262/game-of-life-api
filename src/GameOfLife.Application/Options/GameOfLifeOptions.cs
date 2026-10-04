using Microsoft.Extensions.Options;

namespace GameOfLife.Application;

/// <summary>Limits bound from the <c>GameOfLife</c> configuration section. Values here are the defaults.</summary>
public sealed class GameOfLifeOptions
{
    public const string SectionName = "GameOfLife";

    /// <summary>Default cap on generations walked when resolving a final state.</summary>
    public int MaxGenerations { get; set; } = 1_000;

    /// <summary>
    /// Hard ceiling a caller may not exceed via the maxGenerations query parameter.
    /// Must be at least <see cref="MaxGenerations"/>; a lower ceiling would silently shrink the default.
    /// </summary>
    public int MaxGenerationsCeiling { get; set; } = 10_000;

    /// <summary>Upper bound on width * height. 300 per side. A larger grid is rejected at upload.</summary>
    public int MaxCells { get; set; } = 90_000;

    /// <summary>
    /// Upper bound on cells x generations for one /generations read, which writes a row per step.
    /// /final does not use this. It stores a hash per step and stops at the generation ceiling.
    /// </summary>
    public long MaxCellGenerations { get; set; } = 5_000_000;

    /// <summary>Upper bound on a request body, checked before the grid is built.</summary>
    public int MaxRequestBytes { get; set; } = 2_000_000;
}

/// <summary>Rejects a configuration that would make the limits contradict each other.</summary>
public sealed class GameOfLifeOptionsValidator : IValidateOptions<GameOfLifeOptions>
{
    public ValidateOptionsResult Validate(string? name, GameOfLifeOptions options)
    {
        var failures = new List<string>();
        if (options.MaxGenerations < 1)
        {
            failures.Add("GameOfLife:MaxGenerations must be at least 1");
        }

        if (options.MaxGenerationsCeiling < options.MaxGenerations)
        {
            failures.Add("GameOfLife:MaxGenerationsCeiling must be at least MaxGenerations");
        }

        if (options.MaxCells < 1)
        {
            failures.Add("GameOfLife:MaxCells must be at least 1");
        }

        if (options.MaxCellGenerations < 1)
        {
            failures.Add("GameOfLife:MaxCellGenerations must be at least 1");
        }

        if (options.MaxRequestBytes < 1)
        {
            failures.Add("GameOfLife:MaxRequestBytes must be at least 1");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
