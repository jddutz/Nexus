using System.Numerics;

namespace Nexus.AssetPipeline.Typography.Geometry;

/// <summary>
/// Represents a quadratic Bezier edge with one control point.
/// </summary>
public sealed class QuadraticSegment : Edge
{
    /// <summary>
    /// Initializes a quadratic Bezier segment.
    /// </summary>
    /// <param name="start">The point where the segment begins.</param>
    /// <param name="control">The quadratic control point.</param>
    /// <param name="end">The point where the segment ends.</param>
    public QuadraticSegment(Vector2 start, Vector2 control, Vector2 end)
        : base(start, end)
    {
        Control = control;
    }

    /// <summary>
    /// Gets the quadratic control point.
    /// </summary>
    public Vector2 Control { get; }
}
