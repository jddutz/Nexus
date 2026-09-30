using System.Runtime.InteropServices;
using Nexus.Graphics;
using Nexus.Graphics.Components;
using Nexus.Graphics.Textures;
using Nexus.GUI;
using Nexus.GUI.Elements;
using Silk.NET.Maths;

namespace Tests;

/// <summary>Tests image sizing, source coordinates, clipping, and visual lifecycle.</summary>
public sealed class ImageElementTests
{
    /// <summary>Verifies each sizing mode computes the expected arranged image bounds.</summary>
    [Theory]
    [InlineData(SizingMode.Original, 10f, 21f, 4f, 2f)]
    [InlineData(SizingMode.Fit, 10f, 21f, 4f, 2f)]
    [InlineData(SizingMode.FitHorizontal, 10f, 21f, 4f, 2f)]
    [InlineData(SizingMode.FitVertical, 10f, 20f, 4f, 4f)]
    [InlineData(SizingMode.Fill, 10f, 20f, 4f, 4f)]
    [InlineData(SizingMode.Stretch, 10f, 20f, 4f, 4f)]
    public void Arrange_sizesImageForConfiguredMode(
        SizingMode sizingMode,
        float x,
        float y,
        float width,
        float height
    )
    {
        var element = CreateImageElement(4, 2);
        if (sizingMode != SizingMode.Original)
            element.SizingMode = sizingMode;

        element.Arrange(new Rectangle<float>(10f, 20f, 4f, 4f));

        AssertBounds(new Rectangle<float>(x, y, width, height), element.Bounds);
    }

    /// <summary>Verifies custom sizing uses its independent UV rectangle and validates inputs.</summary>
    [Fact]
    public void SetCustomSizingMode_appliesSizeAndIndependentUvRectangle()
    {
        var element = CreateImageElement(8, 4);
        element.SetCustomSizingMode(new(3f, 2f), new(0.25f, 0.25f, 0.5f, 0.5f));
        element.Arrange(new Rectangle<float>(0f, 0f, 8f, 6f));

        Assert.Equal(new Vector2D<float>(3f, 2f), element.Measure(new(8f, 6f)));
        var quad = ReadQuad(element.GetComponent<TextureComponent>()!, 0);
        Assert.Equal(new Vector4D<float>(0.3125f, 0.375f, 0.375f, 0.25f), quad.TexCoord);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            element.SetCustomSizingMode(Vector2D<float>.Zero, new(0f, 0f, 1f, 1f))
        );
    }

    /// <summary>Verifies clipping remaps UVs proportionally within an atlas source region.</summary>
    [Fact]
    public void Arrange_clipsImageAndRemapsAtlasCoordinates()
    {
        var element = CreateImageElement(8, 4, new Rectangle<int>(2, 1, 4, 2));
        element.SetCustomSizingMode(new(6f, 4f), new(0.25f, 0.25f, 0.5f, 0.5f));
        element.HorizontalAlignment = AlignHorizontal.Center;
        element.VerticalAlignment = AlignVertical.Center;
        element.Arrange(new Rectangle<float>(5f, 7f, 4f, 2f));

        AssertBounds(new Rectangle<float>(5f, 7f, 4f, 2f), element.Bounds);
        var quad = ReadQuad(element.GetComponent<TextureComponent>()!, 0);
        Assert.Equal(new Vector4D<float>(1f / 3f, 0.375f, 1f / 3f, 0.25f), quad.TexCoord);
    }

    /// <summary>Verifies zero-sized arrangement and ancestor visibility control drawable ownership.</summary>
    [Fact]
    public void VisibilityAndZeroSize_removeAndRecreateVisualComponent()
    {
        var parent = new Element();
        var element = CreateImageElement(4, 2);
        element.Arrange(new Rectangle<float>(2f, 3f, 8f, 4f));
        var original = element.GetComponent<TextureComponent>();
        parent.AddChild(element);

        parent.IsVisible = false;
        Assert.Empty(element.Components);
        element.ImageSource = new ImageSource(CreateTexture(6, 3));
        parent.IsVisible = true;

        var recreated = element.GetComponent<TextureComponent>();
        Assert.NotNull(recreated);
        Assert.NotSame(original, recreated);
        AssertBounds(new Rectangle<float>(4f, 3.5f, 6f, 3f), element.Bounds);

        element.Arrange(new Rectangle<float>(0f, 0f, 0f, 4f));
        Assert.Empty(element.Components);
        AssertBounds(new Rectangle<float>(0f, 0f, 0f, 0f), element.Bounds);
    }

    /// <summary>Creates an image element backed by deterministic texture dimensions.</summary>
    /// <param name="width">The texture width.</param>
    /// <param name="height">The texture height.</param>
    /// <param name="sourceRegion">The optional source pixel rectangle.</param>
    /// <returns>The configured image element.</returns>
    private static ImageElement CreateImageElement(
        int width,
        int height,
        Rectangle<int>? sourceRegion = null
    ) => new(new ImageSource(CreateTexture(width, height), sourceRegion));

    /// <summary>Creates a deterministic in-memory RGBA texture.</summary>
    /// <param name="width">The texture width.</param>
    /// <param name="height">The texture height.</param>
    /// <returns>The texture.</returns>
    private static Texture CreateTexture(int width, int height) =>
        new("image-element-tests", (uint)width, (uint)height, new Color[width * height]);

    /// <summary>Asserts each coordinate and extent of a rectangle independently.</summary>
    /// <param name="expected">The expected rectangle.</param>
    /// <param name="actual">The actual rectangle.</param>
    private static void AssertBounds(Rectangle<float> expected, Rectangle<float> actual)
    {
        Assert.Equal(expected.Origin.X, actual.Origin.X);
        Assert.Equal(expected.Origin.Y, actual.Origin.Y);
        Assert.Equal(expected.Size.X, actual.Size.X);
        Assert.Equal(expected.Size.Y, actual.Size.Y);
    }

    /// <summary>Reads one instance transform and UV rectangle from a texture component.</summary>
    /// <param name="component">The component containing the instance.</param>
    /// <param name="index">The zero-based instance index.</param>
    /// <returns>The decoded transform and UV rectangle.</returns>
    private static (Matrix4X4<float> Transform, Vector4D<float> TexCoord) ReadQuad(
        TextureComponent component,
        int index
    )
    {
        var data = new byte[component.GetInstanceData(index, Span<byte>.Empty)];
        component.GetInstanceData(index, data);
        var transformSize = System.Runtime.CompilerServices.Unsafe.SizeOf<Matrix4X4<float>>();
        return (
            MemoryMarshal.Read<Matrix4X4<float>>(data),
            MemoryMarshal.Read<Vector4D<float>>(data.AsSpan(transformSize))
        );
    }
}
