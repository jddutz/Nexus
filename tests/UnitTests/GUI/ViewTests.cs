using Nexus.Graphics;
using Nexus.Graphics.Cameras;
using Nexus.GUI.Elements;
using Silk.NET.Maths;

namespace Tests;

/// <summary>Tests viewport configuration and layout synchronization for GUI views.</summary>
public sealed class ViewTests
{
    /// <summary>Verifies construction attaches exactly one renderer to the view.</summary>
    [Fact]
    public void Constructor_attachesSingleViewRenderer()
    {
        var view = new View();

        Assert.Same(view.ViewComponent, Assert.Single(view.Components.OfType<ViewRenderer>()));
        Assert.Same(view.ViewComponent, Assert.Single(view.Components));
        Assert.Equal(RenderLayers.All, view.LayerMask);
        Assert.Equal(RenderLayers.All, view.ViewComponent.LayerMask);
    }

    /// <summary>Verifies public view configuration is propagated to the owned renderer.</summary>
    [Fact]
    public void Configuration_changes_propagateToRenderer()
    {
        var view = new View();
        var camera = new StaticCamera();

        view.Camera = camera;
        view.LayerMask = 0x25;
        view.SortOrder = 3;
        view.RenderOrder = 7;
        view.PreserveDrawOrder = true;
        view.BlendMode = BlendMode.Additive;

        Assert.Same(camera, view.ViewComponent.Camera);
        Assert.Equal(0x25UL, view.ViewComponent.LayerMask);
        Assert.Equal(7, view.ViewComponent.RenderOrder);
        Assert.True(view.ViewComponent.PreserveDrawOrder);
        Assert.Equal(BlendMode.Additive, view.ViewComponent.BlendMode);
        Assert.Equal(3, view.SortOrder);
    }

    /// <summary>Verifies arranged bounds, including their origin, define the renderer viewport.</summary>
    [Fact]
    public void Arrange_updatesClippingRegionFromFinalBounds()
    {
        var view = new View();
        var bounds = new Rectangle<float>(12f, 18f, 80f, 60f);

        view.Arrange(bounds);

        Assert.Equal(bounds, view.Bounds);
        Assert.Equal(new Rectangle<int>(12, 18, 80, 60), view.ViewComponent.ClippingRegion);
        Assert.True(view.ViewComponent.HasExplicitClippingRegion);
    }

    /// <summary>Verifies fractional bounds are rounded inward so the viewport cannot exceed them.</summary>
    [Fact]
    public void Arrange_roundsFractionalViewportBoundsInward()
    {
        var view = new View();

        view.Arrange(new Rectangle<float>(10.25f, 20.75f, 9.5f, 8.5f));

        Assert.Equal(new Rectangle<int>(11, 21, 8, 8), view.ViewComponent.ClippingRegion);
    }

    /// <summary>Verifies the default fit mode preserves the camera aspect ratio in the viewport.</summary>
    [Fact]
    public void Arrange_fitModePreservesCameraAspectRatio()
    {
        var camera = new OrthoCamera();
        camera.SetSize(200f, 100f);
        var view = new View { Camera = camera };

        view.Arrange(new Rectangle<float>(10f, 20f, 300f, 300f));

        Assert.Equal(new Rectangle<int>(10, 95, 300, 150), view.ViewComponent.ViewportRegion);
        Assert.Equal(new Rectangle<int>(10, 20, 300, 300), view.ViewComponent.ClippingRegion);
    }

    /// <summary>Verifies a zero-area arrangement produces an empty explicit viewport.</summary>
    [Fact]
    public void Arrange_zeroAreaProducesEmptyViewport()
    {
        var view = new View();

        view.Arrange(new Rectangle<float>(10f, 20f, 0f, 0f));

        Assert.Equal(new Rectangle<int>(0, 0, 0, 0), view.ViewComponent.ClippingRegion);
        Assert.True(view.ViewComponent.HasExplicitClippingRegion);
    }

    /// <summary>Verifies arranging the view does not change camera projection dimensions.</summary>
    [Fact]
    public void Arrange_doesNotResizeCamera()
    {
        var camera = new StaticCamera();
        camera.SetViewportSize(640f, 480f);
        var projection = camera.ProjectionMatrix;
        var view = new View { Camera = camera };

        view.Arrange(new Rectangle<float>(30f, 40f, 120f, 90f));

        Assert.Equal(projection, camera.ProjectionMatrix);
    }

    /// <summary>Verifies ancestor visibility suppresses and restores the arranged viewport.</summary>
    [Fact]
    public void AncestorVisibility_controlsViewportRendering()
    {
        var parent = new Element();
        var view = new View();
        parent.Children.Add(view);
        var bounds = new Rectangle<float>(10f, 20f, 100f, 80f);
        view.Arrange(bounds);

        parent.IsVisible = false;
        Assert.Equal(new Rectangle<int>(0, 0, 0, 0), view.ViewComponent.ClippingRegion);

        parent.IsVisible = true;
        Assert.Equal(new Rectangle<int>(10, 20, 100, 80), view.ViewComponent.ClippingRegion);
    }
}
