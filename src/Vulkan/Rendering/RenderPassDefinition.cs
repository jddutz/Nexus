namespace Nexus.Graphics.Vulkan.Rendering;

/// <summary>
/// Describes whether and how one Vulkan render pass is executed for a render layer.
/// </summary>
public class RenderPassDefinition
{
    /// <summary>Gets or sets the single-bit render-pass mask identifying the pass.</summary>
    public uint RenderPass { get; set; }

    /// <summary>Gets or sets a value indicating whether this pass should be rendered.</summary>
    public bool ShouldRender { get; set; }

    /// <summary>Gets or sets the clear values supplied when beginning the pass.</summary>
    public ClearValue[] ClearValues { get; set; } = [];
}
