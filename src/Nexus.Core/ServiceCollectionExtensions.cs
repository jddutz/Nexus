using Microsoft.Extensions.DependencyInjection;

namespace Nexus.Core;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection RegisterEventHandlersFromAssemblyContaining<T>(
        this IServiceCollection services
    )
    {
        return services;
    }
}
