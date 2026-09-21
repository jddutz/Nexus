namespace Nexus.Graphics;

public interface IComputable
{
    DrawableId Id { get; }

    IShaderContract ComputeShader { get; }

    ReadOnlyMemory<byte> GetUniformData(ShaderInput[] layout);
}
