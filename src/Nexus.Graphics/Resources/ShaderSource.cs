namespace Nexus.Graphics.Resources;

public abstract record ShaderSource : IGraphicsResource
{
    public sealed record File(string Path) : ShaderSource
    {
        public override ResourceId Id => new IdentityHashBuilder(nameof(File)).Add(Path).Compute();
    }

    public sealed record Embedded(string AssemblyName, string ResourceName) : ShaderSource
    {
        public override ResourceId Id =>
            new IdentityHashBuilder(nameof(Embedded)).Add(AssemblyName).Add(ResourceName).Compute();
    }

    public abstract ResourceId Id { get; }
}
