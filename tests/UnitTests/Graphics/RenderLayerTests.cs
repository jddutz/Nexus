namespace Nexus.Graphics;

/// <summary>
/// Tests the render-layer configuration and defaults.
/// </summary>
public class RenderLayerTests
{
    /// <summary>
    /// Verifies that the constructor stores every configurable rendering setting.
    /// </summary>
    [Fact]
    public void Constructor_StoresRenderingSettings()
    {
        var layer = new RenderLayer(
            3,
            "Foreground",
            RenderPasses.UI,
            preserveDrawOrder: true,
            blendMode: BlendMode.Alpha,
            enableDepthTest: false,
            enableDepthWrite: false,
            depthComparison: DepthComparison.Always
        );

        Assert.Equal(3, layer.Index);
        Assert.Equal("Foreground", layer.Name);
        Assert.Equal(RenderPasses.UI, layer.RenderPassMask);
        Assert.True(layer.PreserveDrawOrder);
        Assert.Equal(BlendMode.Alpha, layer.BlendMode);
        Assert.False(layer.EnableDepthTest);
        Assert.False(layer.EnableDepthWrite);
        Assert.Equal(DepthComparison.Always, layer.DepthComparison);
    }

    /// <summary>
    /// Verifies that the default GUI layer preserves the existing GUI layer settings.
    /// </summary>
    [Fact]
    public void DefaultGui_UsesDefaultGuiSettings()
    {
        Assert.Equal(
            new RenderLayer(0, "GUI", RenderPasses.Main),
            RenderLayer.DefaultGui
        );
    }
}
