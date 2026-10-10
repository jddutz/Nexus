using Nexus.Core;
using Nexus.Graphics;
using Nexus.Graphics.Cameras;
using Nexus.GUI;
using Nexus.GUI.Elements;
using Silk.NET.Maths;

namespace Tests;

public sealed class ViewAnnotationTests
{
    [Theory]
    [InlineData(ViewSizingMode.Fit, 110f, 170f)]
    [InlineData(ViewSizingMode.Fill, 60f, 170f)]
    [InlineData(ViewSizingMode.Stretch, 110f, 170f)]
    public void ProjectsThroughActualViewport(ViewSizingMode sizing, float x, float y)
    {
        var camera = new OrthoCamera();
        camera.SetSize(200f, 100f);
        var view = new View { Camera = camera, SizingMode = sizing };
        view.Arrange(new(10f, 20f, 400f, 300f));

        Assert.True(view.TryProjectToScreen(new(-50f, 0f, 0f), out var point));
        Assert.Equal(new Vector2D<float>(x, y), point);
    }

    [Fact]
    public void UsesParentTransformAndSignedOffsetsInsteadOfLayoutAllocation()
    {
        var parent = new GameObject2D { Position = new(20f, 30f), Scale = new(2f, 2f) };
        var target = new GameObject2D { Position = new(5f, 10f) };
        parent.Children.Add(target);
        var view = new View { Camera = new StaticCamera(), SizingMode = ViewSizingMode.Stretch };
        view.Camera.SetViewportSize(200f, 100f);
        view.Arrange(new(10f, 20f, 200f, 100f));
        var annotation = new ViewAnnotation
        {
            TargetGameObject = target, View = view,
            LeftOffset = -10f, RightOffset = 40f, TopOffset = -20f, BottomOffset = 10f,
        };
        var child = new Element();
        annotation.Children.Add(child);
        annotation.Arrange(new(0f, 0f, 900f, 900f));

        Assert.Equal(30f, annotation.Bounds.Origin.X, precision: 4);
        Assert.Equal(50f, annotation.Bounds.Origin.Y, precision: 4);
        Assert.Equal(new Vector2D<float>(50f, 30f), annotation.Bounds.Size);
        Assert.Equal(annotation.Bounds, child.Bounds);
    }

    [Fact]
    public void PerspectiveDividesByDepthAndRejectsPointsBehindCamera()
    {
        var camera = new PerspectiveCamera();
        camera.SetViewportSize(400f, 200f);
        var view = new View { Camera = camera };
        view.Arrange(new(10f, 20f, 400f, 200f));
        Assert.True(view.TryProjectToScreen(new(1f, 0f, -5f), out var near));
        Assert.True(view.TryProjectToScreen(new(1f, 0f, -10f), out var far));
        Assert.True(near.X > far.X);
        Assert.True(far.X > 210f);
        Assert.False(view.TryProjectToScreen(new(0f, 0f, 5f), out _));
    }

    [Fact]
    public void OrthographicHelpersRefreshCachedProjection()
    {
        var camera = new OrthoCamera();
        var view = new View { Camera = camera, SizingMode = ViewSizingMode.Stretch };
        view.Arrange(new(0f, 0f, 200f, 100f));
        Assert.True(view.TryProjectToScreen(new(0f, 0f, 0f), out _));
        camera.SetSize(200f, 100f);
        camera.Translate(new(25f, 10f, 0f));
        Assert.True(view.TryProjectToScreen(new(25f, 10f, 0f), out var centered));
        Assert.Equal(new Vector2D<float>(100f, 50f), centered);
        camera.LookAt(new(50f, 20f, 0f));
        Assert.True(view.TryProjectToScreen(new(50f, 20f, 0f), out centered));
        Assert.Equal(new Vector2D<float>(100f, 50f), centered);
    }

    [Fact]
    public void MissingOrHiddenViewCollapsesChildren()
    {
        var child = new Element();
        var annotation = new ViewAnnotation { TargetGameObject = new GameObject3D(), RightOffset = 40f, BottomOffset = 20f };
        annotation.Children.Add(child);
        annotation.Arrange(new(0f, 0f, 800f, 600f));
        Assert.Equal(Vector2D<float>.Zero, child.Bounds.Size);
        annotation.View = new View { Camera = new StaticCamera(), IsVisible = false };
        annotation.Arrange(default);
        Assert.Equal(Vector2D<float>.Zero, child.Bounds.Size);
    }
}
