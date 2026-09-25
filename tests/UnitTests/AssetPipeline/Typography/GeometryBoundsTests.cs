using System.Numerics;
using Nexus.Assets.Typography.Geometry;

namespace Nexus.AssetPipeline.Tests;

/// <summary>
/// Verifies exact axis-aligned bounds for generic line and quadratic geometry.
/// </summary>
public sealed class GeometryBoundsTests
{
    /// <summary>
    /// Includes interior X and Y extrema without including the quadratic control point itself.
    /// </summary>
    [Fact]
    public void GetBounds_usesQuadraticExtremaAndLineEndpoints()
    {
        var contour = new Contour([
            new QuadraticSegment(Vector2.Zero, new Vector2(3f, 3f), new Vector2(2f, 0f)),
            new LineSegment(new Vector2(2f, 0f), Vector2.Zero),
        ]);

        var bounds = GeometryBoundsCalculator.GetBounds([contour]);

        Assert.Equal(new GeometryBounds(0f, 0f, 2.25f, 1.5f), bounds);
    }

    /// <summary>
    /// Returns no bounds when no contour contains an edge.
    /// </summary>
    [Fact]
    public void GetBounds_returnsNullForEmptyGeometry()
    {
        Assert.Null(GeometryBoundsCalculator.GetBounds([]));
        Assert.Null(GeometryBoundsCalculator.GetBounds([new Contour([])]));
    }
}
