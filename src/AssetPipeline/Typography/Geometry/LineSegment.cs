using System.Numerics;

namespace Nexus.AssetPipeline.Typography.Geometry;

/// <summary>
/// Represents a straight edge between two points.
/// </summary>
public sealed class LineSegment : Edge
{
    /// <summary>
    /// Initializes a line segment between two points.
    /// </summary>
    /// <param name="start">The point where the segment begins.</param>
    /// <param name="end">The point where the segment ends.</param>
    public LineSegment(Vector2 start, Vector2 end)
        : base(start, end) { }
}