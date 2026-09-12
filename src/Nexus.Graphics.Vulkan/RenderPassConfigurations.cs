namespace Nexus.Graphics.Vulkan;

/// <summary>
/// Provides the standard Vulkan configuration for each render-pass mask.
/// </summary>
public static class RenderPassConfigurations
{
    /// <summary>
    /// Gets the standard configuration for every render pass, indexed by bit position.
    /// </summary>
    public static RenderPassConfiguration[] Configurations =>
        [
            new()
            {
                Name = nameof(RenderPasses.Shadow),
                ColorFormat = Format.Undefined,
                DepthFormat = Format.D32Sfloat,
                ColorLoadOp = AttachmentLoadOp.DontCare,
                ColorStoreOp = AttachmentStoreOp.DontCare,
                DepthLoadOp = AttachmentLoadOp.Clear,
                DepthStoreOp = AttachmentStoreOp.Store,
                ColorInitialLayout = ImageLayout.Undefined,
                ColorFinalLayout = ImageLayout.ColorAttachmentOptimal,
                DepthInitialLayout = ImageLayout.Undefined,
                DepthFinalLayout = ImageLayout.DepthStencilReadOnlyOptimal,
                SampleCount = SampleCountFlags.Count1Bit,
                BatchStrategy = new DefaultBatchStrategy(),
            },
            new()
            {
                Name = nameof(RenderPasses.Depth),
                ColorFormat = Format.Undefined,
                DepthFormat = Format.D32Sfloat,
                ColorLoadOp = AttachmentLoadOp.DontCare,
                ColorStoreOp = AttachmentStoreOp.DontCare,
                DepthLoadOp = AttachmentLoadOp.Clear,
                DepthStoreOp = AttachmentStoreOp.Store,
                ColorInitialLayout = ImageLayout.Undefined,
                ColorFinalLayout = ImageLayout.ColorAttachmentOptimal,
                DepthInitialLayout = ImageLayout.Undefined,
                DepthFinalLayout = ImageLayout.DepthStencilAttachmentOptimal,
                SampleCount = SampleCountFlags.Count1Bit,
                BatchStrategy = new DefaultBatchStrategy(),
            },
            new()
            {
                Name = nameof(RenderPasses.Main),
                ColorFormat = Format.Undefined,
                DepthFormat = Format.D32Sfloat,
                ColorLoadOp = AttachmentLoadOp.Clear,
                ColorStoreOp = AttachmentStoreOp.Store,
                DepthLoadOp = AttachmentLoadOp.Clear,
                DepthStoreOp = AttachmentStoreOp.Store,
                ColorInitialLayout = ImageLayout.Undefined,
                ColorFinalLayout = ImageLayout.ColorAttachmentOptimal,
                DepthInitialLayout = ImageLayout.Undefined,
                DepthFinalLayout = ImageLayout.DepthStencilAttachmentOptimal,
                SampleCount = SampleCountFlags.Count1Bit,
                BatchStrategy = new DefaultBatchStrategy(),
            },
            new()
            {
                Name = nameof(RenderPasses.Lighting),
                ColorFormat = Format.Undefined,
                DepthFormat = Format.Undefined,
                ColorLoadOp = AttachmentLoadOp.Load,
                ColorStoreOp = AttachmentStoreOp.Store,
                DepthLoadOp = AttachmentLoadOp.DontCare,
                DepthStoreOp = AttachmentStoreOp.DontCare,
                ColorInitialLayout = ImageLayout.ColorAttachmentOptimal,
                ColorFinalLayout = ImageLayout.ColorAttachmentOptimal,
                DepthInitialLayout = ImageLayout.Undefined,
                DepthFinalLayout = ImageLayout.DepthStencilAttachmentOptimal,
                SampleCount = SampleCountFlags.Count1Bit,
                BatchStrategy = new DefaultBatchStrategy(),
            },
            new()
            {
                Name = nameof(RenderPasses.Reflection),
                ColorFormat = Format.Undefined,
                DepthFormat = Format.D32Sfloat,
                ColorLoadOp = AttachmentLoadOp.Load,
                ColorStoreOp = AttachmentStoreOp.Store,
                DepthLoadOp = AttachmentLoadOp.Load,
                DepthStoreOp = AttachmentStoreOp.Store,
                ColorInitialLayout = ImageLayout.ColorAttachmentOptimal,
                ColorFinalLayout = ImageLayout.ColorAttachmentOptimal,
                DepthInitialLayout = ImageLayout.DepthStencilAttachmentOptimal,
                DepthFinalLayout = ImageLayout.DepthStencilAttachmentOptimal,
                SampleCount = SampleCountFlags.Count1Bit,
                BatchStrategy = new DefaultBatchStrategy(),
            },
            new()
            {
                Name = nameof(RenderPasses.Transparent),
                ColorFormat = Format.Undefined,
                DepthFormat = Format.D32Sfloat,
                ColorLoadOp = AttachmentLoadOp.Load,
                ColorStoreOp = AttachmentStoreOp.Store,
                DepthLoadOp = AttachmentLoadOp.Load,
                DepthStoreOp = AttachmentStoreOp.DontCare,
                ColorInitialLayout = ImageLayout.ColorAttachmentOptimal,
                ColorFinalLayout = ImageLayout.ColorAttachmentOptimal,
                DepthInitialLayout = ImageLayout.DepthStencilAttachmentOptimal,
                DepthFinalLayout = ImageLayout.DepthStencilAttachmentOptimal,
                SampleCount = SampleCountFlags.Count1Bit,
                BatchStrategy = new DefaultBatchStrategy(),
            },
            new()
            {
                Name = nameof(RenderPasses.Post),
                ColorFormat = Format.Undefined,
                DepthFormat = Format.Undefined,
                ColorLoadOp = AttachmentLoadOp.Load,
                ColorStoreOp = AttachmentStoreOp.Store,
                DepthLoadOp = AttachmentLoadOp.DontCare,
                DepthStoreOp = AttachmentStoreOp.DontCare,
                ColorInitialLayout = ImageLayout.ColorAttachmentOptimal,
                ColorFinalLayout = ImageLayout.ColorAttachmentOptimal,
                DepthInitialLayout = ImageLayout.Undefined,
                DepthFinalLayout = ImageLayout.DepthStencilAttachmentOptimal,
                SampleCount = SampleCountFlags.Count1Bit,
                BatchStrategy = new DefaultBatchStrategy(),
            },
            new()
            {
                Name = nameof(RenderPasses.UI),
                ColorFormat = Format.Undefined,
                DepthFormat = Format.Undefined,
                ColorLoadOp = AttachmentLoadOp.Load,
                ColorStoreOp = AttachmentStoreOp.Store,
                DepthLoadOp = AttachmentLoadOp.DontCare,
                DepthStoreOp = AttachmentStoreOp.DontCare,
                ColorInitialLayout = ImageLayout.ColorAttachmentOptimal,
                ColorFinalLayout = ImageLayout.PresentSrcKhr,
                DepthInitialLayout = ImageLayout.Undefined,
                DepthFinalLayout = ImageLayout.DepthStencilAttachmentOptimal,
                SampleCount = SampleCountFlags.Count1Bit,
                BatchStrategy = new DefaultBatchStrategy(),
            },
        ];
}
