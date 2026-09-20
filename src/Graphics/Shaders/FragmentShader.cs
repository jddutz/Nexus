namespace Nexus.Graphics.Shaders;

public sealed record FragmentShader : Shader
{
    public ColorFormatEnum ColorFormat { get; }

    public FragmentShader(
        string name,
        string sourceFileName,
        PrimitiveTopologyEnum topology,
        VertexFormat vertexFormat,
        ColorFormatEnum colorFormat
    )
        : base(name, sourceFileName, ShaderStageEnum.Fragment, topology, vertexFormat)
    {
        ColorFormat = colorFormat;
    }
}
