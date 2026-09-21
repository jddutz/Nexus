namespace Nexus.Graphics;

public interface IComputable
{
    RenderableId Id { get; }

    IShaderContract ComputeShader { get; }

    ReadOnlyMemory<byte> GetUniformData(ShaderInput[] layout);
}
