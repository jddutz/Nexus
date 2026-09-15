namespace Nexus.Graphics.Vulkan.Resources;

public class ShaderRegistry(IShaderFactory factory) : IResourceRegistry
{
    private readonly HashSet<ResourceId> _dirty = [];

    public bool CanLoad(IResourceDescription resource) => resource is ShaderResourceDescription;

    public ResourceId Load(IResourceDescription resource)
    {
        if (resource is not ShaderResourceDescription shader)
            throw new ArgumentException(
                $"{nameof(ShaderRegistry)} cannot load {resource.GetType().Name}.",
                nameof(resource)
            );

        var definition =
            ResourceDefinitions.ShaderDefinitions.SingleOrDefault(d => d.Name == shader.Name)
            ?? throw new InvalidOperationException(
                $"No Vulkan shader definition exists for '{shader.Name}'."
            );

        var id = factory.Create(definition);

        _dirty.Add(id);

        return id;
    }
}
