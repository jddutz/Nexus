namespace Nexus.Graphics.Vulkan.Resources;

public class TextureRegistry : IResourceRegistry
{
    private readonly HashSet<ResourceId> _dirty = [];

    public bool CanLoad(IResourceDescription resource) => resource is TextureResourceDescription;

    public ResourceId Load(IResourceDescription resource)
    {
        if (resource is not TextureResourceDescription texture)
            throw new ArgumentException(
                $"{nameof(TextureRegistry)} cannot load {resource.GetType().Name}.",
                nameof(resource)
            );

        // TODO:
        // - Translate TextureResourceDescription -> Vulkan TextureDefinition.
        // - Pass the definition to ITextureFactory.Create().
        // - Add the returned ResourceId to _dirty.
        // - Return the ResourceId to the caller.

        throw new NotImplementedException("Vulkan texture loading is not implemented.");
    }
}
