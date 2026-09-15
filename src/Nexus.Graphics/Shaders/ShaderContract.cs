namespace Nexus.Graphics.Shaders;

public interface IShaderContract
{
    PrimitiveTopologyEnum Topology { get; }
    VertexDefinition VertexDefinition { get; }

    IReadOnlyList<VertexInput> VertexInputs { get; }
    // Later:
    // InstanceDefinition
    // Uniforms / descriptors
    // Push constants
    // textures / samplers
    // etc.
}
