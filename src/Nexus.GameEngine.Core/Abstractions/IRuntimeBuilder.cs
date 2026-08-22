namespace Nexus.GameEngine.Core.Abstractions;

public interface IRuntimeBuilder
{
    IRuntimeBuilder Use<TService>(TService instance)
        where TService : class;

    IRuntimeBuilder Use<TService, TImplementation>()
        where TService : class
        where TImplementation : class, TService;

    IRuntimeBuilder Use<TService>(Func<IServiceProvider, TService> factory)
        where TService : class;

    IRuntimeBuilder Remove<TService>()
        where TService : class;

    bool Contains<TService>()
        where TService : class;

    Runtime Build();
}
