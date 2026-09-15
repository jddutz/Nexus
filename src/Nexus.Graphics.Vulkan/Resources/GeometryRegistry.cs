namespace Nexus.Graphics.Vulkan.Resources;

public class GeometryRegistry(IGeometryFactory factory) : IResourceRegistry
{
    private readonly HashSet<ResourceId> _dirty = [];

    public bool CanLoad(IResourceDescription resource) =>
        resource is GeometryResourceDescription or VertexGeometryResourceDescription;

    public ResourceId Load(IResourceDescription resource)
    {
        return resource switch
        {
            VertexGeometryResourceDescription vertices => Load(vertices),

            GeometryResourceDescription => throw new NotImplementedException(
                "File-based geometry loading is not implemented."
            ),

            _ => throw new ArgumentException(
                $"{nameof(GeometryRegistry)} cannot load {resource.GetType().Name}.",
                nameof(resource)
            ),
        };
    }

    private ResourceId Load(VertexGeometryResourceDescription resource)
    {
        var definition = new UniformColorVertexGeometryDefinition([.. resource.Vertices]);

        var id = factory.Create(definition);

        _dirty.Add(id);

        return id;
    }
}
