using System.Runtime.InteropServices;
using Nexus.Graphics;
using Nexus.Graphics.Shaders;
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
    [InlineData(ImageSizingMode.Original, 10f, 21f, 4f, 2f)]
    [InlineData(ImageSizingMode.Fit, 10f, 21f, 4f, 2f)]
    [InlineData(ImageSizingMode.FitHorizontal, 10f, 21f, 4f, 2f)]
    [InlineData(ImageSizingMode.FitVertical, 10f, 20f, 4f, 4f)]
    [InlineData(ImageSizingMode.Fill, 10f, 20f, 4f, 4f)]
    [InlineData(ImageSizingMode.Stretch, 10f, 20f, 4f, 4f)]
    public void Arrange_sizesImageForConfiguredMode(
        ImageSizingMode sizingMode,
        float x,
        float y,
        float width,
        float height
    )
    {
        var element = CreateImageElement(4, 2);
        if (sizingMode != ImageSizingMode.Original)
            element.SizingMode = sizingMode;

        element.Arrange(new Rectangle<float>(10f, 20f, 4f, 4f));

        AssertBounds(new Rectangle<float>(x, y, width, height), element.Bounds);
    }

    /// <summary>Verifies margins limit image sizing and keep visuals inside the content bounds.</summary>
    [Fact]
    public void Margins_limitMeasurementAndImageRendering()
    {
        var element = CreateImageElement(4, 2);
        element.Margins = new Margins(1f, 2f, 3f, 4f);
        var allocation = new Rectangle<float>(10f, 20f, 10f, 10f);

        Assert.Equal(new Vector2D<float>(7f, 9f), element.Measure(new(10f, 10f)));
        element.Arrange(allocation);

        var expectedImageBounds = new Rectangle<float>(12.5f, 23.5f, 4f, 2f);
        AssertBounds(expectedImageBounds, element.Bounds);
        Assert.Equal(
            expectedImageBounds,
            (Rectangle<float>)element.GetComponent<TextureRenderer>()!.Destination
        );
    }

    /// <summary>Verifies image dimensions are capped by the element's requested size.</summary>
    [Fact]
    public void WidthAndHeight_capMeasuredAndArrangedImageSize()
    {
        var element = CreateImageElement(4, 2);
        element.Width = 2f;
        element.Height = 1f;

        Assert.Equal(new Vector2D<float>(2f, 1f), element.Measure(new(10f, 10f)));
        element.Arrange(new Rectangle<float>(10f, 20f, 10f, 10f));

        var expectedBounds = new Rectangle<float>(14f, 24.5f, 2f, 1f);
        AssertBounds(expectedBounds, element.Bounds);
        Assert.Equal(
            expectedBounds,
            (Rectangle<float>)element.GetComponent<TextureRenderer>()!.Destination
        );
    }

    /// <summary>Verifies custom sizing uses its independent UV rectangle and validates inputs.</summary>
    [Fact]
    public void SetCustomSizingMode_appliesSizeAndIndependentUvRectangle()
    {
        var element = CreateImageElement(8, 4);
        element.SetCustomSizingMode(new(3f, 2f), new(0.25f, 0.25f, 0.5f, 0.5f));
        element.Arrange(new Rectangle<float>(0f, 0f, 8f, 6f));

        Assert.Equal(new Vector2D<float>(3f, 2f), element.Measure(new(8f, 6f)));
        var quad = ReadQuad(element.GetComponent<TextureRenderer>()!, 0);
        Assert.Equal(new Vector4D<float>(0.25f, 0.25f, 0.5f, 0.5f), quad.TexCoord);
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
        var quad = ReadQuad(element.GetComponent<TextureRenderer>()!, 0);
        Assert.Equal(1f / 3f, quad.TexCoord.X, 6);
        Assert.Equal(0.375f, quad.TexCoord.Y, 6);
        Assert.Equal(1f / 3f, quad.TexCoord.Z, 6);
        Assert.Equal(0.25f, quad.TexCoord.W, 6);
    }

    /// <summary>Verifies the element validates source regions against its assigned texture.</summary>
    [Fact]
    public void SourceRegion_rejectsInvalidRectangles()
    {
        var element = CreateImageElement(8, 4);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            element.SourceRegion = new Rectangle<int>(-1, 0, 2, 2)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            element.SourceRegion = new Rectangle<int>(7, 0, 2, 2)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            element.SourceRegion = new Rectangle<int>(0, 0, 0, 2)
        );
    }

    /// <summary>Verifies source regions can be configured before their texture is assigned.</summary>
    [Fact]
    public void SourceRegion_isValidatedWhenTextureIsAssigned()
    {
        var element = new ImageElement { SourceRegion = new Rectangle<int>(0, 0, 5, 3) };

        Assert.Throws<ArgumentOutOfRangeException>(() => element.Texture = CreateTexture(4, 2));
        Assert.Null(element.Texture);

        element.Texture = CreateTexture(6, 4);
        Assert.Equal(new Rectangle<int>(0, 0, 5, 3), element.SourceRegion);
    }

    /// <summary>Verifies zero-sized arrangement and ancestor visibility control drawable ownership.</summary>
    [Fact]
    public void VisibilityAndZeroSize_removeAndRecreateVisualComponent()
    {
        var parent = new Element();
        var element = CreateImageElement(4, 2);
        element.Arrange(new Rectangle<float>(2f, 3f, 8f, 4f));
        var original = element.GetComponent<TextureRenderer>();
        parent.AddChild(element);

        parent.IsVisible = false;
        Assert.Empty(element.Components);
        Assert.Equal(Vector2D<float>.Zero, element.Bounds.Size);
        element.Texture = CreateTexture(6, 3);
        parent.IsVisible = true;
        element.Arrange(new Rectangle<float>(2f, 3f, 8f, 4f));

        var recreated = element.GetComponent<TextureRenderer>();
        Assert.NotNull(recreated);
        Assert.NotSame(original, recreated);
        AssertBounds(new Rectangle<float>(3f, 3.5f, 6f, 3f), element.Bounds);

        element.Arrange(new Rectangle<float>(0f, 0f, 0f, 4f));
        Assert.Empty(element.Components);
        AssertBounds(new Rectangle<float>(0f, 0f, 0f, 0f), element.Bounds);
    }

    /// <summary>Verifies an image without a source remains empty until one is assigned.</summary>
    [Fact]
    public void MissingTexture_keepsImageElementEmptyUntilAssigned()
    {
        var element = new ImageElement();

        Assert.Equal(Vector2D<float>.Zero, element.Measure(new(10f, 10f)));
        element.Arrange(new Rectangle<float>(0f, 0f, 10f, 10f));
        Assert.Empty(element.Components);
        AssertBounds(new Rectangle<float>(0f, 0f, 0f, 0f), element.Bounds);

        element.Texture = CreateTexture(4, 2);
        element.Arrange(new Rectangle<float>(0f, 0f, 10f, 10f));

        Assert.NotNull(element.GetComponent<TextureRenderer>());
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
    ) => new() { Texture = CreateTexture(width, height), SourceRegion = sourceRegion };

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
        TextureRenderer component,
        int index
    )
    {
        var layout = BuiltInShaders.TexturedQuadVertexShader.InstanceLayout;
        var data = new byte[checked(layout.Sum(input => input.Size))];
        Assert
            .Single(component.Drawables)
            .WriteInstanceDataTo(checked((ulong)index), 1, layout, data);
        var transformSize = System.Runtime.CompilerServices.Unsafe.SizeOf<Matrix4X4<float>>();
        return (
            MemoryMarshal.Read<Matrix4X4<float>>(data),
            MemoryMarshal.Read<Vector4D<float>>(data.AsSpan(transformSize))
        );
    }
}
