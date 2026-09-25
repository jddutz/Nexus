using System.Numerics;
using Nexus.Assets.Typography.Geometry;

namespace Nexus.AssetPipeline.Tests;

/// <summary>
/// Verifies signed distances with synthetic line and quadratic geometry.
/// </summary>
public sealed class GeometryDistanceTests
{
    /// <summary>
    /// Verifies oriented line distance for interior projections and clamped endpoints.
    /// </summary>
    [Fact]
    public void SignedDistanceToEdge_measuresLineSegments()
    {
        var edge = new LineSegment(Vector2.Zero, new Vector2(2f, 0f));

        Assert.Equal(1f, GeometryDistance.SignedDistanceToEdge(new Vector2(1f, 1f), edge));
        Assert.Equal(-1f, GeometryDistance.SignedDistanceToEdge(new Vector2(1f, -1f), edge));
        Assert.Equal(1f, GeometryDistance.SignedDistanceToEdge(new Vector2(-1f, 0f), edge));
        Assert.Equal(0f, GeometryDistance.SignedDistanceToEdge(new Vector2(1f, 0f), edge));
        Assert.Equal(
            -1f,
            GeometryDistance.SignedDistanceToEdge(
                new Vector2(1f, 1f),
                new LineSegment(edge.End, edge.Start)
            )
        );
    }

    /// <summary>
    /// Verifies a curved quadratic edge finds an interior nearest point and an endpoint minimum.
    /// </summary>
    [Fact]
    public void SignedDistanceToEdge_measuresQuadraticSegments()
    {
        var edge = new QuadraticSegment(Vector2.Zero, new Vector2(1f, 2f), new Vector2(2f, 0f));

        Assert.Equal(0.5f, GeometryDistance.SignedDistanceToEdge(new Vector2(1f, 1.5f), edge), 5);
        Assert.Equal(-0.5f, GeometryDistance.SignedDistanceToEdge(new Vector2(1f, 0.5f), edge), 5);
        Assert.Equal(1f, GeometryDistance.SignedDistanceToEdge(new Vector2(-1f, 0f), edge), 5);
        Assert.Equal(0f, GeometryDistance.SignedDistanceToEdge(new Vector2(1f, 1f), edge), 5);
    }

    /// <summary>
    /// Verifies inside, outside, boundary, and known-distance behavior for a square contour.
    /// </summary>
    [Fact]
    public void SignedDistanceToContour_handlesSquare()
    {
        var contour = CreateSquare();

        Assert.Equal(-1f, GeometryDistance.SignedDistanceToContour(Vector2.Zero, contour));
        Assert.Equal(1f, GeometryDistance.SignedDistanceToContour(new Vector2(2f, 0f), contour));
        Assert.Equal(0f, GeometryDistance.SignedDistanceToContour(new Vector2(1f, 0.25f), contour));
        Assert.Equal(
            -0.25f,
            GeometryDistance.SignedDistanceToContour(new Vector2(0.75f, 0f), contour)
        );
    }

    /// <summary>
    /// Verifies winding-based fill behavior for a contour made entirely from quadratic edges.
    /// </summary>
    [Fact]
    public void SignedDistanceToContour_handlesQuadraticCircleLikeContour()
    {
        var contour = CreateQuadraticCircleLikeContour();

        Assert.True(GeometryDistance.SignedDistanceToContour(Vector2.Zero, contour) < 0f);
        Assert.True(GeometryDistance.SignedDistanceToContour(new Vector2(2f, 0f), contour) > 0f);
        Assert.Equal(
            0f,
            GeometryDistance.SignedDistanceToContour(new Vector2(0.75f, 0.75f), contour),
            5
        );
        Assert.Equal(
            MathF.Sqrt(0.02f),
            GeometryDistance.SignedDistanceToContour(new Vector2(0.85f, 0.85f), contour),
            4
        );
    }

    /// <summary>
    /// Verifies oppositely oriented contours create a hole under the nonzero winding rule.
    /// </summary>
    [Fact]
    public void SignedDistanceToShape_respectsContourOrientationForHoles()
    {
        var shape = new[] { CreateSquare(2f), CreateSquare(1f, reverse: true) };

        Assert.True(GeometryDistance.SignedDistanceToShape(new Vector2(1.5f, 0f), shape) < 0f);
        Assert.True(GeometryDistance.SignedDistanceToShape(Vector2.Zero, shape) > 0f);
    }

    /// <summary>
    /// Creates an axis-aligned square contour with configurable orientation and extent.
    /// </summary>
    /// <param name="halfExtent">The distance from the center to each side.</param>
    /// <param name="reverse">Whether to reverse the contour orientation.</param>
    /// <returns>The square contour.</returns>
    private static Contour CreateSquare(float halfExtent = 1f, bool reverse = false)
    {
        var points = new[]
        {
            new Vector2(-halfExtent, -halfExtent),
            new Vector2(halfExtent, -halfExtent),
            new Vector2(halfExtent, halfExtent),
            new Vector2(-halfExtent, halfExtent),
        };
        if (reverse)
            Array.Reverse(points);

        return new Contour(
            points.Select(
                (point, index) => new LineSegment(point, points[(index + 1) % points.Length])
            )
        );
    }

    /// <summary>
    /// Creates a closed rounded-square contour from four quadratic arcs.
    /// </summary>
    /// <returns>The quadratic contour.</returns>
    private static Contour CreateQuadraticCircleLikeContour() =>
        new([
            new QuadraticSegment(new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f)),
            new QuadraticSegment(new Vector2(0f, 1f), new Vector2(-1f, 1f), new Vector2(-1f, 0f)),
            new QuadraticSegment(new Vector2(-1f, 0f), new Vector2(-1f, -1f), new Vector2(0f, -1f)),
            new QuadraticSegment(new Vector2(0f, -1f), new Vector2(1f, -1f), new Vector2(1f, 0f)),
        ]);
}
