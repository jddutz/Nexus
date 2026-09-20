namespace Nexus.Graphics.Shaders;

/// <summary>Identifies the programmable stage represented by a shader contract.</summary>
public enum ShaderStageEnum
{
    /// <summary>Indicates that no shader stage is specified.</summary>
    Undefined,

    /// <summary>Indicates a vertex shader.</summary>
    Vertex,

    /// <summary>Indicates a tessellation-control shader.</summary>
    TessellationControl,

    /// <summary>Indicates a tessellation-evaluation shader.</summary>
    TessellationEval,

    /// <summary>Indicates a geometry shader.</summary>
    Geometry,

    /// <summary>Indicates a fragment shader.</summary>
    Fragment,

    /// <summary>Indicates a compute shader.</summary>
    Compute,
}
