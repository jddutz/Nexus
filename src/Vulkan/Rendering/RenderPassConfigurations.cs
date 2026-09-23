namespace Nexus.Graphics.Vulkan.Rendering;

/// <summary>
/// Provides the render-pass configurations used to create swap-chain render passes.
/// </summary>
public sealed class RenderPassConfigurations
{
    private readonly Dictionary<int, RenderPassConfiguration> _configurations;

    /// <summary>
    /// Initializes a new instance of the <see cref="RenderPassConfigurations"/> class with the default render-pass configuration.
    /// </summary>
    public RenderPassConfigurations()
        : this(
            new Dictionary<int, RenderPassConfiguration>
            {
                [RenderPasses.GetIndex(RenderPasses.Main)] = new()
                {
                    RenderPassBit = RenderPasses.Main,
                    ShouldRender = true,
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
                },
            }
        ) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="RenderPassConfigurations"/> class with the supplied configurations.
    /// </summary>
    /// <param name="configurations">The render-pass configurations indexed by render-pass position.</param>
    public RenderPassConfigurations(IDictionary<int, RenderPassConfiguration> configurations)
    {
        ArgumentNullException.ThrowIfNull(configurations);
        _configurations = new Dictionary<int, RenderPassConfiguration>(configurations);
    }

    /// <summary>
    /// Gets the render-pass configurations indexed by render-pass position.
    /// </summary>
    public IReadOnlyDictionary<int, RenderPassConfiguration> Configurations => _configurations;
}
