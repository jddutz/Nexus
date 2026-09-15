namespace Nexus.Graphics.Resources;

public static partial class BuiltInResource
{
    public static readonly IResourceDescription UniformColorVertexShader =
        new ShaderResourceDescription("UniformColorVertexShader", ShaderStageEnum.Vertex);
    public static readonly IResourceDescription UniformColorFragmentShader =
        new ShaderResourceDescription("UniformColorFragmentShader", ShaderStageEnum.Fragment);
}
