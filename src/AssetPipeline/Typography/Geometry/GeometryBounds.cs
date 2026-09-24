namespace Nexus.AssetPipeline.Typography.Geometry;

/// <summary>
/// Describes the axis-aligned bounds of geometry in its original coordinate space.
/// </summary>
/// <param name="Left">The minimum X coordinate.</param>
/// <param name="Bottom">The minimum Y coordinate.</param>
/// <param name="Right">The maximum X coordinate.</param>
/// <param name="Top">The maximum Y coordinate.</param>
public readonly record struct GeometryBounds(float Left, float Bottom, float Right, float Top);

/// <summary>
/// Calculates axis-aligned bounds for line and quadratic contour geometry.
/// </summary>
public static class GeometryBoundsCalculator
{
    /// <summary>
    /// Gets the smallest axis-aligned bounds containing all supplied curves.
    /// </summary>
    /// <param name="contours">The contours whose edge geometry is bounded.</param>
    /// <returns>The bounds, or null when the contours contain no edges.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="contours"/> is null.</exception>
    public static GeometryBounds? GetBounds(IEnumerable<Contour> contours)
    {
        ArgumentNullException.ThrowIfNull(contours);
        GeometryBounds? bounds = null;
        foreach (var contour in contours)
        {
            ArgumentNullException.ThrowIfNull(contour);
            foreach (var edge in contour.Edges)
            {
                ArgumentNullException.ThrowIfNull(edge);
                bounds = Include(bounds, edge.Start);
                bounds = Include(bounds, edge.End);
                if (edge is QuadraticSegment quadratic)
                    bounds = IncludeQuadraticExtrema(bounds!.Value, quadratic);
            }
        }

        return bounds;
    }

    /// <summary>
    /// Adds a point to the current bounds.
    /// </summary>
    /// <param name="bounds">The bounds accumulated so far, if any.</param>
    /// <param name="point">The point to include.</param>
    /// <returns>The updated bounds.</returns>
    private static GeometryBounds Include(GeometryBounds? bounds, System.Numerics.Vector2 point) =>
        bounds is { } current
            ? new GeometryBounds(
                MathF.Min(current.Left, point.X),
                MathF.Min(current.Bottom, point.Y),
                MathF.Max(current.Right, point.X),
                MathF.Max(current.Top, point.Y)
            )
            : new GeometryBounds(point.X, point.Y, point.X, point.Y);

    /// <summary>
    /// Includes each interior extremum of a quadratic curve.
    /// </summary>
    /// <param name="bounds">The bounds accumulated so far.</param>
    /// <param name="curve">The quadratic curve to inspect.</param>
    /// <returns>The updated bounds.</returns>
    private static GeometryBounds IncludeQuadraticExtrema(
        GeometryBounds bounds,
        QuadraticSegment curve
    )
    {
        var start = curve.Start;
        var control = curve.Control;
        var end = curve.End;
        var xDenominator = start.X - 2f * control.X + end.X;
        if (xDenominator != 0f)
        {
            var xT = (start.X - control.X) / xDenominator;
            if (xT is > 0f and < 1f)
                bounds = Include(bounds, Evaluate(start, control, end, xT));
        }

        var yDenominator = start.Y - 2f * control.Y + end.Y;
        if (yDenominator != 0f)
        {
            var yT = (start.Y - control.Y) / yDenominator;
            if (yT is > 0f and < 1f)
                bounds = Include(bounds, Evaluate(start, control, end, yT));
        }

        return bounds;
    }

    /// <summary>
    /// Evaluates a quadratic Bezier curve at the specified parameter.
    /// </summary>
    /// <param name="start">The start point.</param>
    /// <param name="control">The control point.</param>
    /// <param name="end">The end point.</param>
    /// <param name="t">The curve parameter.</param>
    /// <returns>The point on the curve at <paramref name="t"/>.</returns>
    private static System.Numerics.Vector2 Evaluate(
        System.Numerics.Vector2 start,
        System.Numerics.Vector2 control,
        System.Numerics.Vector2 end,
        float t
    )
    {
        var inverseT = 1f - t;
        return inverseT * inverseT * start + 2f * inverseT * t * control + t * t * end;
    }
}
