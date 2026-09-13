namespace Nexus.Graphics.Vulkan;

public class VulkanResourceManager : IGraphicsResourceManager
{
    private readonly Dictionary<ResourceId, IGraphicsResource> _definitions = [];
    private readonly Dictionary<ResourceId, IGraphicsResource> _resources = [];

    public void Register(IGraphicsResource definition)
    {
        _definitions.Add(definition.Id, definition);
    }

    public bool TryGet<T>(ResourceId id, out T? resource)
        where T : class, IGraphicsResource
    {
        resource = null;

        if (_resources.TryGetValue(id, out var existing))
        {
            resource = (T)existing;
            return true;
        }

        if (!_definitions.TryGetValue(id, out var definition))
        {
            return false;
        }

        resource = Load(definition) as T;

        if (resource == null)
        {
            return false;
        }

        _resources.Add(id, resource);

        return true;
    }

    public T GetOrCreate<T>(ResourceId id)
        where T : class, IGraphicsResource
    {
        if (_resources.TryGetValue(id, out var existing))
        {
            return (T)existing;
        }

        if (!_definitions.TryGetValue(id, out var definition))
        {
            throw new KeyNotFoundException($"Graphics resource '{id}' is not registered.");
        }

        var resource = Load(definition);

        if (resource is T typed)
        {
            _resources.Add(id, typed);
        }
        else
        {
            throw new InvalidOperationException(
                $"Resource {id} is not of type {typeof(T).Name} (actual: {resource.GetType().Name})."
            );
        }

        return (T)resource;
    }

    public IEnumerable<IGraphicsResource> GetResourceDefinitions() => _definitions.Values;

    private object Load(IGraphicsResource definition)
    {
        // FIXME: this isn't going to work!
        return definition;
    }
}
