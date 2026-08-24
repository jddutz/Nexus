namespace Nexus.Graphics.Vulkan.Pipelines;

/// <summary>
/// Immutable wrapper containing both a Vulkan Pipeline and its associated PipelineLayout.
/// Internal to the Vulkan backend - PipelineManager converts this to/from the backend-agnostic
/// <see cref="Nexus.Graphics.PipelineHandle"/> at the Nexus.Graphics.Abstractions boundary.
/// </summary>
/// <param name="Pipeline">The Vulkan graphics pipeline handle</param>
/// <param name="Layout">The Vulkan pipeline layout handle (needed for push constants and descriptor sets)</param>
/// <param name="Name">Human-readable name for debugging and identification</param>
public readonly record struct VulkanPipelineHandle(
    Pipeline Pipeline,
    PipelineLayout Layout,
    string Name
)
{
    /// <summary>
    /// Shader stages active for this pipeline. Populated from the pipeline descriptor
    /// so runtime code (renderer) can use it as a default when draw commands don't
    /// provide an explicit override.
    /// </summary>
    public ShaderStageFlags ShaderStageFlags { get; init; } =
        ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit;

    /// <summary>
    /// Returns true if this handle contains valid (non-zero) pipeline and layout handles.
    /// </summary>
    public bool IsValid => Pipeline.Handle != 0 && Layout.Handle != 0;

    /// <summary>
    /// Returns an invalid/empty pipeline handle.
    /// </summary>
    public static VulkanPipelineHandle Invalid => new(default, default, "Invalid");

    /// <summary>Converts to the backend-agnostic handle exposed across the Abstractions boundary.</summary>
    public Nexus.Graphics.PipelineHandle ToPipelineHandle() =>
        new(new GpuHandle(Pipeline.Handle), new GpuHandle(Layout.Handle), Name)
        {
            ShaderStageFlags = ShaderStageFlags,
        };
}
