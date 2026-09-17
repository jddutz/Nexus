namespace Nexus.Graphics.Vulkan;

/// <summary>
/// Provides the standard Vulkan configuration for each render-pass mask.
/// </summary>
public static class RenderPassConfigurations
{
    /// <summary>
    /// Gets the standard configuration for the currently supported render pass, indexed by bit position.
    /// </summary>
    public static IReadOnlyDictionary<int, RenderPassConfiguration> Configurations =>
        new Dictionary<int, RenderPassConfiguration>
        {
            [RenderPasses.GetIndex(RenderPasses.Main)] = new()
            {
                Name = nameof(RenderPasses.Main),
                ColorFormat = Format.Undefined,
                DepthFormat = Format.Undefined,
                ColorLoadOp = AttachmentLoadOp.Clear,
                ColorStoreOp = AttachmentStoreOp.Store,
                DepthLoadOp = AttachmentLoadOp.DontCare,
                DepthStoreOp = AttachmentStoreOp.DontCare,
                ColorInitialLayout = ImageLayout.Undefined,
                ColorFinalLayout = ImageLayout.PresentSrcKhr,
                DepthInitialLayout = ImageLayout.Undefined,
                DepthFinalLayout = ImageLayout.DepthStencilAttachmentOptimal,
                SampleCount = SampleCountFlags.Count1Bit,
                BatchStrategy = new DefaultBatchStrategy(),
            },
        };
}
