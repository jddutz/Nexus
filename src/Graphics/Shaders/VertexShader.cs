namespace Nexus.Graphics.Shaders;

public sealed record VertexShader : Shader
{
    public PrimitiveTopologyEnum Topology { get; }
    public InstanceLayout InstanceLayout { get; }

    public VertexShader(
        string name,
        string sourceFileName,
        PrimitiveTopologyEnum topology,
        VertexFormat vertexFormat,
        InstanceLayout instanceLayout
    )
        : base(name, sourceFileName, ShaderStageEnum.Vertex, topology, vertexFormat)
    {
        Topology = topology;
        InstanceLayout = instanceLayout;
    }
}
