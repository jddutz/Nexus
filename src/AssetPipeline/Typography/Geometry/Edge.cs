using System.Numerics;

namespace Nexus.AssetPipeline.Typography.Geometry;

/// <summary>
/// Represents one directed segment in a closed font contour.
/// </summary>
public abstract class Edge
{
    /// <summary>
    /// Initializes an edge from its starting point to its ending point.
    /// </summary>
    /// <param name="start">The point where the edge begins.</param>
    /// <param name="end">The point where the edge ends.</param>
    protected Edge(Vector2 start, Vector2 end)
    {
        Start = start;
        End = end;
    }

    /// <summary>
    /// Gets the point where the edge begins.
    /// </summary>
    public Vector2 Start { get; }

    /// <summary>
    /// Gets the point where the edge ends.
    /// </summary>
    public Vector2 End { get; }
}
