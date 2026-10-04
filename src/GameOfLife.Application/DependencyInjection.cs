using Microsoft.Extensions.DependencyInjection;

namespace GameOfLife.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ICreateBoard, CreateBoardHandler>();
        services.AddScoped<IGetBoard, GetBoardHandler>();
        services.AddScoped<IGetGeneration, GetGenerationHandler>();
        services.AddScoped<IGetFinalState, GetFinalStateHandler>();
        return services;
    }
}
