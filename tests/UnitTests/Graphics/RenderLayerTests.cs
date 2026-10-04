namespace Nexus.Graphics;

/// <summary>
/// Tests the render-layer configuration and defaults.
/// </summary>
public class RenderLayerTests
{
    /// <summary>
    /// Verifies that a render layer stores its classification properties.
    /// </summary>
    [Fact]
    public void Constructor_StoresRenderingSettings()
    {
        var layer = new RenderLayer(3, "Foreground", RenderPasses.UI);

        Assert.Equal(3, layer.Index);
        Assert.Equal("Foreground", layer.Name);
        Assert.Equal(RenderPasses.UI, layer.RenderPassMask);
    }

    /// <summary>
    /// Verifies the default GUI layer remains a classification for the main pass.
    /// </summary>
    [Fact]
    public void DefaultGui_UsesDefaultGuiSettings()
    {
        Assert.Equal(new RenderLayer(0, "GUI", RenderPasses.Main), RenderLayer.DefaultGui);
    }

    /// <summary>
    /// Verifies the view owns render-order and pipeline-state configuration.
    /// </summary>
    [Fact]
    public void ViewComponent_StoresRenderingPolicy()
    {
        var view = new Nexus.Graphics.Components.ViewRenderer
        {
            PreserveDrawOrder = true,
            BlendMode = BlendMode.PremultipliedAlpha,
            EnableDepthTest = false,
            EnableDepthWrite = false,
            DepthComparison = DepthComparison.Greater,
        };

        Assert.True(view.PreserveDrawOrder);
        Assert.Equal(BlendMode.PremultipliedAlpha, view.BlendMode);
        Assert.False(view.EnableDepthTest);
        Assert.False(view.EnableDepthWrite);
        Assert.Equal(DepthComparison.Greater, view.DepthComparison);
    }
}
