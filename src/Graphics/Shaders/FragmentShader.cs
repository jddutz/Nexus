namespace Nexus.Graphics.Shaders;

public sealed record FragmentShader : Shader
{
    public ColorFormatEnum ColorFormat { get; }

    public FragmentShader(
        string name,
        string sourceFileName,
        VertexFormat vertexFormat,
        ColorFormatEnum colorFormat,
        ShaderInput[] uniformInputs,
        ShaderInput[] instanceInputs
    )
        : base(
            name,
            sourceFileName,
            ShaderStageEnum.Fragment,
            vertexFormat,
            uniformInputs,
            instanceInputs
        )
    {
        ColorFormat = colorFormat;
    }
}
