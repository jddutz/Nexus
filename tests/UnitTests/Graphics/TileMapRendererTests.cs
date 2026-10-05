using System;
using System.Runtime.InteropServices;
using Nexus.Core;
using Nexus.Graphics;
using Nexus.Graphics.Components;
using Nexus.Graphics.Drawables;
using Nexus.Graphics.Geometry;
using Nexus.Graphics.Shaders;
using Silk.NET.Maths;

namespace Nexus.UnitTests.Graphics;

/// <summary>Verifies sparse tile-map data and batched renderer synchronization.</summary>
public sealed class TileMapRendererTests
{
    private static readonly TileMapCell WhiteCell = new(
        new Vector4D<float>(0f, 0f, 1f, 1f),
        Colors.White
    );

    /// <summary>Verifies sparse writes, no-op mutations, bounds shrink, and clear behavior.</summary>
    [Fact]
    public void TileMapData_mutations_preserve_bounds_and_advance_only_when_changed()
    {
        var map = new TileMapData(new Rectangle<int>(-2, 3, 4, 2));
        var notifications = 0;
        map.Changed += (_, _) => notifications++;

        map.SetCell(-2, 3, WhiteCell);
        Assert.Equal(1, map.Revision);
        map.SetCell(-2, 3, WhiteCell);
        Assert.Equal(1, map.Revision);
        map.SetCell(-2, 3, new TileMapCell(new Vector4D<float>(0f, 0f, 1f, 1f), Colors.Red));
        Assert.Equal(2, map.Revision);
        Assert.False(map.RemoveCell(1, 4));
        Assert.Equal(2, map.Revision);
        Assert.Throws<ArgumentOutOfRangeException>(() => map.SetCell(2, 3, WhiteCell));

        map.CellBounds = new Rectangle<int>(-1, 3, 2, 2);
        Assert.Empty(map.Cells);
        Assert.Equal(new Rectangle<int>(-1, 3, 2, 2), map.CellBounds);
        Assert.Equal(3, map.Revision);
        Assert.Equal(3, notifications);

        map.SetCell(0, 4, WhiteCell);
        map.Clear();
        Assert.Empty(map.Cells);
        Assert.Equal(new Rectangle<int>(-1, 3, 2, 2), map.CellBounds);
        Assert.Equal(5, map.Revision);
        Assert.Equal(5, notifications);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TileMapData(new Rectangle<int>(0, 0, -1, 1))
        );
        Assert.Throws<ArgumentOutOfRangeException>(() => map.SetCell(0, 3, default));
    }

    /// <summary>Verifies derived bounds include negative origins and cell size.</summary>
    [Fact]
    public void LocalBounds_include_cell_origin_and_cell_size()
    {
        var renderer = new TileMapRenderer
        {
            Map = new TileMapData(new Rectangle<int>(-2, 3, 4, 2)),
            CellSize = new Vector2D<float>(10f, 8f),
        };

        Assert.Equal(new Rectangle<float>(-20f, 24f, 40f, 16f), renderer.LocalBounds);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            renderer.CellSize = new Vector2D<float>(0f, 1f)
        );
    }

    /// <summary>Verifies cell transforms compose with owner world transforms using shared geometry.</summary>
    [Fact]
    public void Renderer_serializes_cells_using_owner_world_transform()
    {
        var map = new TileMapData(new Rectangle<int>(-1, 2, 3, 2));
        map.SetCell(-1, 2, WhiteCell);
        map.SetCell(1, 3, WhiteCell);
        var renderer = new TileMapRenderer
        {
            Map = map,
            CellSize = new Vector2D<float>(8f, 6f),
        };
        var owner = new GameObject2D([renderer])
        {
            Position = new Vector2D<float>(100f, 50f),
            Scale = new Vector2D<float>(2f, 3f),
        };
        renderer.Activate();

        var drawable = Assert.IsType<TileMapDrawable>(Assert.Single(renderer.Drawables));
        Assert.Equal(BuiltInGeometry.TexturedQuadOffset.Id, drawable.Mesh.Id);
        var instances = ReadInstances(drawable);

        Assert.Equal(2, instances.Length);
        Assert.Equal(84f, instances[0].M41);
        Assert.Equal(86f, instances[0].M42);
        Assert.Equal(16f, instances[0].M11);
        Assert.Equal(18f, instances[0].M22);
    }

    /// <summary>Verifies edits are published as one snapshot without replacing the drawable.</summary>
    [Fact]
    public void Active_edits_publish_without_drawable_lifecycle_churn()
    {
        var map = new TileMapData(new Rectangle<int>(0, 0, 3, 1));
        var renderer = new TileMapRenderer { Map = map };
        var additions = 0;
        var removals = 0;
        renderer.DrawableAdded += (_, _) => additions++;
        renderer.DrawableRemoved += (_, _) => removals++;
        renderer.Activate();

        var drawable = Assert.IsType<TileMapDrawable>(Assert.Single(renderer.Drawables));
        map.SetCell(0, 0, WhiteCell);
        map.SetCell(1, 0, WhiteCell);
        map.RemoveCell(0, 0);
        renderer.Update(0d);

        Assert.Same(drawable, Assert.Single(renderer.Drawables));
        Assert.Equal(1UL, drawable.InstanceCount);
        Assert.Equal(1, additions);
        Assert.Equal(0, removals);
    }

    /// <summary>Verifies zero-instance maps can become populated and empty without reactivation.</summary>
    [Fact]
    public void Empty_to_populated_and_back_keeps_drawable_registered()
    {
        var map = new TileMapData(new Rectangle<int>(0, 0, 1, 1));
        var renderer = new TileMapRenderer { Map = map };
        renderer.Activate();
        var drawable = Assert.IsType<TileMapDrawable>(Assert.Single(renderer.Drawables));
        Assert.Equal(0UL, drawable.InstanceCount);

        map.SetCell(0, 0, WhiteCell);
        renderer.Update(0d);
        Assert.Same(drawable, Assert.Single(renderer.Drawables));
        Assert.Equal(1UL, drawable.InstanceCount);

        map.Clear();
        renderer.Update(0d);
        Assert.Same(drawable, Assert.Single(renderer.Drawables));
        Assert.Equal(0UL, drawable.InstanceCount);
    }

    /// <summary>Verifies per-cell texture regions and tint are serialized in each instance.</summary>
    [Fact]
    public void Drawable_serializes_cell_texture_region_and_tint()
    {
        var region = new Vector4D<float>(0.25f, 0.5f, 0.125f, 0.25f);
        var cell = new TileMapCell(region, Colors.Red);
        var map = new TileMapData(new Rectangle<int>(0, 0, 1, 1));
        map.SetCell(0, 0, cell);
        var renderer = new TileMapRenderer { Map = map };
        renderer.Activate();
        var drawable = Assert.IsType<TileMapDrawable>(Assert.Single(renderer.Drawables));
        var record = new byte[96];

        drawable.WriteInstanceDataTo(
            0,
            1,
            BuiltInShaders.TexturedQuadVertexShader.InstanceLayout,
            record
        );

        Assert.Equal(
            region,
            MemoryMarshal.Read<Vector4D<float>>(new ReadOnlySpan<byte>(record, 64, 16))
        );
        Assert.Equal(Colors.Red, MemoryMarshal.Read<Color>(new ReadOnlySpan<byte>(record, 80, 16)));
    }

    /// <summary>Verifies reactivation synchronizes changes made while inactive.</summary>
    [Fact]
    public void Reactivation_uses_latest_map_snapshot()
    {
        var map = new TileMapData(new Rectangle<int>(0, 0, 2, 1));
        var renderer = new TileMapRenderer { Map = map };
        renderer.Activate();
        renderer.Deactivate();
        map.SetCell(1, 0, WhiteCell);
        renderer.Activate();

        Assert.Equal(1UL, Assert.IsType<TileMapDrawable>(Assert.Single(renderer.Drawables)).InstanceCount);
    }

    /// <summary>Verifies active map replacement detaches the old data source.</summary>
    [Fact]
    public void Map_replacement_updates_existing_drawable_and_unsubscribes_previous_map()
    {
        var oldMap = new TileMapData(new Rectangle<int>(0, 0, 1, 1));
        var newMap = new TileMapData(new Rectangle<int>(0, 0, 2, 1));
        var renderer = new TileMapRenderer { Map = oldMap };
        renderer.Activate();
        var drawable = Assert.IsType<TileMapDrawable>(Assert.Single(renderer.Drawables));

        newMap.SetCell(1, 0, WhiteCell);
        renderer.Map = newMap;
        renderer.Update(0d);
        oldMap.SetCell(0, 0, WhiteCell);
        renderer.Update(0d);

        Assert.Same(drawable, Assert.Single(renderer.Drawables));
        Assert.Equal(1UL, drawable.InstanceCount);
    }

    /// <summary>Verifies each renderer tracks changes to shared data independently.</summary>
    [Fact]
    public void Renderers_sharing_map_data_publish_independently()
    {
        var map = new TileMapData(new Rectangle<int>(0, 0, 2, 1));
        var first = new TileMapRenderer { Map = map };
        var second = new TileMapRenderer { Map = map };
        first.Activate();
        second.Activate();

        map.SetCell(0, 0, WhiteCell);
        first.Update(0d);
        map.SetCell(1, 0, WhiteCell);
        second.Update(0d);

        Assert.Equal(1UL, Assert.IsType<TileMapDrawable>(Assert.Single(first.Drawables)).InstanceCount);
        Assert.Equal(2UL, Assert.IsType<TileMapDrawable>(Assert.Single(second.Drawables)).InstanceCount);
        Assert.False(typeof(TextureRenderer).IsAssignableFrom(typeof(TileMapRenderer)));
    }

    /// <summary>Reads the drawable's packed transform, texture-region, and tint records.</summary>
    /// <param name="drawable">The tile-map drawable to serialize.</param>
    /// <returns>The transforms serialized for its current cells.</returns>
    private static Matrix4X4<float>[] ReadInstances(TileMapDrawable drawable)
    {
        var layout = BuiltInShaders.TexturedQuadVertexShader.InstanceLayout;
        var stride = checked((int)layout.Sum(input => (long)input.Size));
        var bytes = new byte[checked(stride * (int)drawable.InstanceCount)];
        drawable.WriteInstanceDataTo(0, drawable.InstanceCount, layout, bytes);
        var transforms = new Matrix4X4<float>[checked((int)drawable.InstanceCount)];
        for (var index = 0; index < transforms.Length; index++)
            transforms[index] = MemoryMarshal.Read<Matrix4X4<float>>(
                new ReadOnlySpan<byte>(bytes, index * stride, 64)
            );

        return transforms;
    }
}
