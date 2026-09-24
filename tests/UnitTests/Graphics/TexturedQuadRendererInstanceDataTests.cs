using System.Runtime.InteropServices;
using Nexus.Graphics;
using Nexus.Graphics.Shaders;
using Silk.NET.Maths;

namespace Nexus.UnitTests.Graphics;

/// <summary>
/// Verifies the pre-Vulkan instance serialization performed by <see cref="TexturedQuadRenderer"/>.
/// </summary>
public sealed class TexturedQuadRendererInstanceDataTests
{
    /// <summary>
    /// Verifies that the demo grid serializes one complete, distinct, and source-equivalent record per quad.
    /// </summary>
    [Fact]
    public void GetInstanceData_DemoGrid_ProducesCompleteDistinctSourceEquivalentRecords()
    {
        var layout = BuiltInShaders.TexturedQuadVertexShader.InstanceLayout;
        var renderers = CreateDemoGrid();
        var drawables = renderers.Select(renderer => (IDrawable)renderer).ToArray();
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

        for (var index = 0; index < renderers.Length; index++)
        {
            var record = records[index];
            var transform = MemoryMarshal.Read<Matrix4X4<float>>(record.AsSpan(transformOffset));
            var textureRegion = MemoryMarshal.Read<Vector4D<float>>(
                record.AsSpan(textureRegionOffset)
            );
            var color = MemoryMarshal.Read<Color>(record.AsSpan(colorOffset));

            Assert.Equal(renderers[index].TransformationMatrix, transform);
            Assert.Equal(renderers[index].TextureRegion, textureRegion);
            Assert.Equal(renderers[index].Color, color);
            transforms.Add(transform);
        }

        Assert.Equal(renderers.Length, transforms.Count);
    }

    /// <summary>
    /// Creates the textured-quad data used by the HelloNexus demo grid.
    /// </summary>
    /// <returns>The configured renderers in grid order.</returns>
    private static TexturedQuadRenderer[] CreateDemoGrid()
    {
        const int columns = 16;
        const int rows = 9;
        const int atlasColumns = 14;
        const int atlasRows = 13;

        var cellWidth = 2.0f / columns;
        var cellHeight = 2.0f / rows;
        var regionWidth = 1.0f / atlasColumns;
        var regionHeight = 1.0f / atlasRows;
        var renderers = new TexturedQuadRenderer[columns * rows];

        for (var x = 0; x < columns; x++)
        {
            for (var y = 0; y < rows; y++)
            {
                var index = x * rows + y;
                var atlasIndex = index % (atlasColumns * atlasRows);
                var centerX = -1.0f + (x + 0.5f) * cellWidth;
                var centerY = -1.0f + (y + 0.5f) * cellHeight;
                var renderer = new TexturedQuadRenderer(centered: true)
                {
                    TextureRegion = new(
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
