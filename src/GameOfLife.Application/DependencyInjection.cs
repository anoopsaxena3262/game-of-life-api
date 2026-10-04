using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace GameOfLife.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<IValidateOptions<GameOfLifeOptions>, GameOfLifeOptionsValidator>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<BoardService>();
        return services;
    }
}
