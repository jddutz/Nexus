namespace Nexus.Graphics.Resources;

public interface IGraphicsResourceManager
{
    void Register(IGraphicsResource definition);

    IEnumerable<IGraphicsResource> GetResourceDefinitions();

    bool TryGet<T>(ResourceId id, out T? resource)
        where T : class, IGraphicsResource;

    T GetOrCreate<T>(ResourceId id)
        where T : class, IGraphicsResource;
}
