using Nexus.Core;
using Nexus.Graphics;
using Nexus.Graphics.Components;
using Nexus.GUI;
using Nexus.GUI.Elements;
using Silk.NET.Maths;

namespace Tests;

/// <summary>Tests tile-map measurement, rendering, and visibility through a GUI element.</summary>
public sealed class TileMapElementTests
{
    /// <summary>Fits map geometry into the allocation and offsets negative cell coordinates.</summary>
    [Fact]
    public void Arrange_fits_map_and_offsets_its_declared_cell_origin()
    {
        var map = CreateMap(new Rectangle<int>(-2, 3, 4, 2));
        var element = new TileMapElement { Map = map, CellSize = new(8f, 6f) };

        element.Arrange(new Rectangle<float>(10f, 20f, 16f, 12f));

        var (renderObject, renderer) = GetRenderer(element);
        Assert.Equal(new Vector2D<float>(10f, 23f), element.Bounds.Origin);
        Assert.Equal(new Vector2D<float>(16f, 6f), element.Bounds.Size);
        Assert.Equal(new Vector2D<float>(18f, 14f), renderObject.Position);
        Assert.Equal(new Vector2D<float>(4f, 3f), renderer.CellSize);
        Assert.True(renderer.IsVisible);

        renderer.Activate();
        var drawable = Assert.Single(renderer.Drawables);
        Assert.IsType<TileMapDrawable>(drawable);
    }

    /// <summary>Measures nominal tile dimensions and caps them to the available content size.</summary>
    [Fact]
    public void Measure_fits_map_to_constraint_and_accounts_for_margins()
    {
        var element = new TileMapElement
        {
            Map = CreateMap(new Rectangle<int>(0, 0, 4, 2)),
            CellSize = new(8f, 6f),
            Margins = new Margins(1f, 2f, 3f, 4f),
        };

        Assert.Equal(new Vector2D<float>(10f, 9.625f), element.Measure(new(10f, 10f)));
        Assert.Equal(Vector2D<float>.Zero, new TileMapElement().Measure(new(10f, 10f)));
    }

    /// <summary>Hides the renderer with the element or any GUI ancestor and restores it afterward.</summary>
    [Fact]
    public void Renderer_visibility_tracks_element_and_ancestor_visibility()
    {
        var parent = new Element();
        var element = new TileMapElement { Map = CreateMap(new Rectangle<int>(0, 0, 1, 1)) };
        parent.AddChild(element);
        element.Arrange(new Rectangle<float>(0f, 0f, 4f, 4f));
        var (_, renderer) = GetRenderer(element);

        parent.IsVisible = false;
        Assert.False(renderer.IsVisible);
        parent.IsVisible = true;
        Assert.True(renderer.IsVisible);
        element.IsVisible = false;
        Assert.False(renderer.IsVisible);
        element.IsVisible = true;
        Assert.True(renderer.IsVisible);
    }

    /// <summary>Rejects invalid tile sizes.</summary>
    [Fact]
    public void Cell_size_requires_positive_finite_dimensions()
    {
        var element = new TileMapElement();

        Assert.Throws<ArgumentOutOfRangeException>(() => element.CellSize = new(0f, 1f));
        Assert.Throws<ArgumentOutOfRangeException>(() => element.CellSize = new(float.NaN, 1f));
    }

    /// <summary>Creates a map with one occupied cell for renderer assertions.</summary>
    /// <param name="bounds">The declared map bounds.</param>
    /// <returns>The populated map.</returns>
    private static TileMapData CreateMap(Rectangle<int> bounds)
    {
        var map = new TileMapData(bounds);
        if (bounds.Size.X > 0 && bounds.Size.Y > 0)
            map.SetCell(bounds.Origin.X, bounds.Origin.Y, new TileMapCell(
                new Rectangle<float>(0f, 0f, 1f, 1f),
                Colors.White
            ));

        return map;
    }

    /// <summary>Gets the spatial render object and tile renderer owned by an element.</summary>
    /// <param name="element">The element under test.</param>
    /// <returns>The owned render object and its tile renderer.</returns>
    private static (GameObject2D RenderObject, TileMapRenderer Renderer) GetRenderer(
        TileMapElement element
    )
    {
        var renderObject = Assert.IsType<GameObject2D>(Assert.Single(element.Children));
        var renderer = Assert.IsType<TileMapRenderer>(
            Assert.Single(renderObject.Components.OfType<TileMapRenderer>())
        );
        return (renderObject, renderer);
    }
}
