namespace Nexus.Graphics;

public interface IComputable
{
    GraphicsId Id { get; }

    IShaderContract ComputeShader { get; }

    ReadOnlyMemory<byte> GetUniformData(ShaderInput[] layout);
}
