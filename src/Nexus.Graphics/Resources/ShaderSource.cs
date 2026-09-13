namespace Nexus.Graphics.Resources;

public record ShaderSource(string Path) : IGraphicsResource
{
    public ResourceId Id => new IdentityHashBuilder(nameof(File)).Add(Path).Compute();
}
