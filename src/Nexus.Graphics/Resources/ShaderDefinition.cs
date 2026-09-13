namespace Nexus.Graphics.Resources;

public sealed class ShaderDefinition : IGraphicsResource
{
    public ResourceId Id { get; }
    public ShaderSource VertexShader { get; }
    public ShaderSource FragmentShader { get; }
}
