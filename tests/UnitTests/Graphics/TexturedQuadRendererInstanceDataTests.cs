using System.Runtime.InteropServices;
using Nexus.Graphics;
using Nexus.Graphics.Shaders;
using Silk.NET.Maths;

namespace Nexus.UnitTests.Graphics;

using Nexus.Graphics.Textures;

/// <summary>
/// Verifies the pre-Vulkan instance serialization performed by textured graphics components.
/// </summary>
public sealed class TextureComponentInstanceDataTests
{
    /// <summary>
    /// Verifies that the demo grid serializes one complete, distinct, and source-equivalent record per quad.
    /// </summary>
    [Fact]
    public void GetInstanceData_DemoGrid_ProducesCompleteDistinctSourceEquivalentRecords()
    {
        var layout = BuiltInShaders.TexturedQuadVertexShader.InstanceLayout;
        var components = CreateDemoGrid();
        var drawables = components.Select(component => (IDrawable)component).ToArray();
        var records = drawables
            .Select(drawable => drawable.GetInstanceData(layout).ToArray())
            .ToArray();

        Assert.Equal(144, records.Length);
        Assert.Equal(
            drawables.Length,
            drawables.Select(drawable => drawable.Id).Distinct().Count()
        );
        Assert.Equal(
            drawables.Length,
            drawables.Select(drawable => drawable.Id).Distinct().Count()
        );
        Assert.All(records, record => Assert.Equal(96, record.Length));
        Assert.Equal(records.Length, records.Distinct(ByteArrayComparer.Instance).Count());

        var transformOffset = GetOffset(layout, InputSemantics.Transform);
        var textureRegionOffset = GetOffset(layout, InputSemantics.TextureRegion);
        var colorOffset = GetOffset(layout, InputSemantics.Color);
        var transforms = new HashSet<Matrix4X4<float>>();

        for (var index = 0; index < components.Length; index++)
        {
            var record = records[index];
            var transform = MemoryMarshal.Read<Matrix4X4<float>>(record.AsSpan(transformOffset));
            var textureRegion = MemoryMarshal.Read<Vector4D<float>>(
                record.AsSpan(textureRegionOffset)
            );
            var color = MemoryMarshal.Read<Color>(record.AsSpan(colorOffset));

            Assert.Equal(components[index].TransformationMatrix, transform);
            Assert.Equal(components[index].TexCoord, textureRegion);
            Assert.Equal(components[index].Color, color);
            transforms.Add(transform);
        }

        Assert.Equal(components.Length, transforms.Count);
    }

    /// <summary>
    /// Verifies that textured-quad mutations raise the event matching the changed render input.
    /// </summary>
    [Fact]
    public void PropertyChanges_raise_matching_drawable_events()
    {
        var renderer = new TextureComponent();
        var renderLayerChanges = 0;
        var textureChanges = 0;
        var instanceDataChanges = 0;
        var uniformDataChanges = 0;

        renderer.RenderLayerChanged += (_, _) => renderLayerChanges++;
        renderer.TextureChanged += (_, _) => textureChanges++;
        renderer.InstanceDataChanged += (_, _) => instanceDataChanges++;
        renderer.UniformDataChanged += (_, _) => uniformDataChanges++;

        renderer.RenderLayerMask = 2;
        renderer.SamplingBehavior = SamplingBehaviors.PixelPerfect;
        renderer.TexCoord = new(0.1f, 0.2f, 0.3f, 0.4f);
        renderer.View = Matrix4X4.CreateTranslation(1f, 2f, 0f);

        Assert.Equal(1, renderLayerChanges);
        Assert.Equal(1, textureChanges);
        Assert.Equal(1, instanceDataChanges);
        Assert.Equal(1, uniformDataChanges);
    }

    /// <summary>
    /// Creates the textured-quad data used by the HelloNexus demo grid.
    /// </summary>
    /// <returns>The configured renderers in grid order.</returns>
    private static TextureComponent[] CreateDemoGrid()
    {
        const int columns = 16;
        const int rows = 9;
        const int atlasColumns = 14;
        const int atlasRows = 13;

        var cellWidth = 2.0f / columns;
        var cellHeight = 2.0f / rows;
        var regionWidth = 1.0f / atlasColumns;
        var regionHeight = 1.0f / atlasRows;
        var renderers = new TextureComponent[columns * rows];

        for (var x = 0; x < columns; x++)
        {
            for (var y = 0; y < rows; y++)
            {
                var index = x * rows + y;
                var atlasIndex = index % (atlasColumns * atlasRows);
                var centerX = -1.0f + (x + 0.5f) * cellWidth;
                var centerY = -1.0f + (y + 0.5f) * cellHeight;
                var renderer = new TextureComponent(centered: true)
                {
                    TexCoord = new(
                        (atlasIndex % atlasColumns) * regionWidth,
                        (atlasIndex / atlasColumns) * regionHeight,
                        regionWidth,
                        regionHeight
                    ),
                    TransformationMatrix =
                        Matrix4X4.CreateScale(cellWidth * 0.9f, cellHeight * 0.9f, 1.0f)
                        * Matrix4X4.CreateTranslation(centerX, centerY, 0f),
                };

                renderers[index] = renderer;
            }
        }

        return renderers;
    }

    /// <summary>Verifies nine-patch packing and border fitting for a destination smaller than its borders.</summary>
    [Fact]
    public void NinePatch_packs_nine_regions_and_fits_borders_to_small_destinations()
    {
        var component = new NinePatchComponent
        {
            Texture = new Texture("test", 100, 80, new Color[100 * 80]),
            Size = new(15f, 12f),
            TexCoord = new(0.2f, 0.1f, 0.5f, 0.5f),
            SourceBorders = new(10f, 8f, 10f, 8f),
        };
        var drawable = (IDrawable)component;
        var layout = BuiltInShaders.TexturedQuadVertexShader.InstanceLayout;
        var data = drawable.GetInstanceData(layout).ToArray();
        var transformOffset = GetOffset(layout, InputSemantics.Transform);
        var textureRegionOffset = GetOffset(layout, InputSemantics.TextureRegion);

        Assert.Equal(9UL, drawable.InstanceCount);
        Assert.Equal(9 * 96, data.Length);

        var topLeftTransform = MemoryMarshal.Read<Matrix4X4<float>>(
            data.AsSpan(transformOffset, 64)
        );
        var topLeftRegion = MemoryMarshal.Read<Vector4D<float>>(
            data.AsSpan(textureRegionOffset, 16)
        );
        var centerRegion = MemoryMarshal.Read<Vector4D<float>>(
            data.AsSpan(4 * 96 + textureRegionOffset, 16)
        );

        Assert.Equal(7.5f, topLeftTransform.M11);
        Assert.Equal(6f, topLeftTransform.M22);
        Assert.Equal(0.205f, topLeftRegion.X, 6);
        Assert.Equal(0.10625f, topLeftRegion.Y, 6);
        Assert.Equal(0.095f, topLeftRegion.Z, 6);
        Assert.Equal(0.09375f, topLeftRegion.W, 6);
        Assert.Equal(0.3f, centerRegion.X);
        Assert.Equal(0.2f, centerRegion.Y);
        Assert.Equal(0.3f, centerRegion.Z);
        Assert.Equal(0.3f, centerRegion.W);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            component.SourceBorders = new(26f, 0f, 25f, 0f)
        );
    }

    /// <summary>Verifies destination caps can preserve source corner aspect ratio.</summary>
    [Fact]
    public void NinePatch_usesIndependentDestinationBorderSizes()
    {
        var component = new NinePatchComponent
        {
            Texture = new Texture("capsule", 384, 128, new Color[384 * 128]),
            Size = new(160f, 38f),
            SourceBorders = new(64f, 64f, 64f, 64f),
            DestinationBorders = new(19f, 19f, 19f, 19f),
        };
        var drawable = (IDrawable)component;
        var layout = BuiltInShaders.TexturedQuadVertexShader.InstanceLayout;
        var data = drawable.GetInstanceData(layout).ToArray();
        var transformOffset = GetOffset(layout, InputSemantics.Transform);
        var textureRegionOffset = GetOffset(layout, InputSemantics.TextureRegion);
        var topLeftTransform = MemoryMarshal.Read<Matrix4X4<float>>(
            data.AsSpan(transformOffset, 64)
        );
        var topLeftRegion = MemoryMarshal.Read<Vector4D<float>>(
            data.AsSpan(textureRegionOffset, 16)
        );

        Assert.Equal(19f, topLeftTransform.M11);
        Assert.Equal(19f, topLeftTransform.M22);
        Assert.Equal(63.5f / 384f, topLeftRegion.Z, 6);
        Assert.Equal(63.5f / 128f, topLeftRegion.W, 6);
    }

    /// <summary>
    /// Gets the byte offset of an input semantic in an instance layout.
    /// </summary>
    /// <param name="layout">The shader instance layout.</param>
    /// <param name="semantic">The semantic whose offset is required.</param>
    /// <returns>The byte offset of <paramref name="semantic"/>.</returns>
    private static int GetOffset(ShaderInput[] layout, int semantic)
    {
        var offset = 0;
        foreach (var input in layout)
        {
            if (input.Semantic == semantic)
                return offset;

            offset += checked((int)input.Size);
        }

        throw new ArgumentException(
            $"The layout does not contain semantic {semantic}.",
            nameof(layout)
        );
    }

    /// <summary>
    /// Compares byte arrays by value for record-uniqueness assertions.
    /// </summary>
    private sealed class ByteArrayComparer : IEqualityComparer<byte[]>
    {
        /// <summary>
        /// Gets the singleton byte-array comparer instance.
        /// </summary>
        public static ByteArrayComparer Instance { get; } = new();

        /// <inheritdoc/>
        public bool Equals(byte[]? left, byte[]? right) =>
            ReferenceEquals(left, right)
            || (left is not null && right is not null && left.AsSpan().SequenceEqual(right));

        /// <inheritdoc/>
        public int GetHashCode(byte[] value)
        {
            var hash = new HashCode();
            foreach (var item in value)
                hash.Add(item);

            return hash.ToHashCode();
        }
    }
}
