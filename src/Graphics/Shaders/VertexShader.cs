namespace Nexus.Graphics.Shaders;

public sealed record VertexShader : Shader
{
    public PrimitiveTopologyEnum Topology { get; }

    public VertexShader(
        string name,
        string sourceFileName,
        PrimitiveTopologyEnum topology,
        VertexFormat vertexFormat,
        ShaderInput[] uniformInputs,
        ShaderInput[] instanceInputs
    )
        : base(
            name,
            sourceFileName,
            ShaderStageEnum.Vertex,
            vertexFormat,
            uniformInputs,
            instanceInputs
        )
    {
        Topology = topology;
    }
}
