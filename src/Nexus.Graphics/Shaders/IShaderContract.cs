namespace Nexus.Graphics.Shaders;

public interface IShaderContract
{
    ResourceId Id { get; }
    string Name { get; }

    PrimitiveTopologyEnum Topology { get; }
    VertexDescription VertexDescription { get; }

    // Later:
    // InstanceDefinition
    // Uniforms / descriptors
    // Push constants
    // textures / samplers
    // etc.
}
