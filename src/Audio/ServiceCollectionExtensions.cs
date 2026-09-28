namespace Nexus.Audio;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Registers services owned by Nexus.Audio.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the default audio system.
    /// </summary>
    /// <param name="services">The service collection to update.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddAudioServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IAudioSystem, AudioSystem>();
        return services;
    }
}