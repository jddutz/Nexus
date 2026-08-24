namespace Nexus.Graphics;

/// <summary>
/// Backend-agnostic handle to a graphics pipeline and its layout, used by <see cref="DrawCommand"/>.
/// Backends resolve <see cref="Pipeline"/>/<see cref="Layout"/> to their native pipeline objects.
/// </summary>
public readonly record struct PipelineHandle(GpuHandle Pipeline, GpuHandle Layout, string Name)
{
    /// <summary>
    /// Shader stages active for this pipeline, used as the default for push-constant uploads.
    /// </summary>
    public ShaderStageFlags ShaderStageFlags { get; init; } =
        ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit;

    /// <summary>Returns true if this handle contains valid (non-zero) pipeline and layout handles.</summary>
    public bool IsValid => Pipeline.IsValid && Layout.IsValid;

    /// <summary>Returns an invalid/empty pipeline handle.</summary>
    public static PipelineHandle Invalid => new(GpuHandle.Invalid, GpuHandle.Invalid, "Invalid");
}
