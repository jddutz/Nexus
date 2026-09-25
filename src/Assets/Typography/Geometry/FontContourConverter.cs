using System.Numerics;
using Nexus.Assets.Typography.FontReader;

namespace Nexus.Assets.Typography.Geometry;

/// <summary>
/// Converts TrueType font contours into generic line and quadratic geometry.
/// </summary>
public static class FontContourConverter
{
    /// <summary>
    /// Converts a font contour, adding implied on-curve points between adjacent off-curve points.
    /// </summary>
    /// <param name="fontContour">The TrueType contour to convert.</param>
    /// <returns>The contour represented by generic geometry edges.</returns>
    public static Contour Convert(FontContour fontContour)
    {
        ArgumentNullException.ThrowIfNull(fontContour);
        var sourcePoints = fontContour.Points;
        if (sourcePoints.Count == 0)
            return new Contour([]);

        var points = new List<(Vector2 Position, bool OnCurve)>(sourcePoints.Count * 2);
        for (var index = 0; index < sourcePoints.Count; index++)
        {
            var point = sourcePoints[index];
            var position = new Vector2(point.X, point.Y);
            points.Add((position, point.OnCurve));

            var next = sourcePoints[(index + 1) % sourcePoints.Count];
            if (!point.OnCurve && !next.OnCurve)
            {
                points.Add(
                    (
                        new Vector2(((float)point.X + next.X) / 2f, ((float)point.Y + next.Y) / 2f),
                        true
                    )
                );
            }
        }

        var startIndex = points.FindIndex(point => point.OnCurve);
        var edges = new List<Edge>(points.Count);
        var currentIndex = startIndex;
        var remainingPoints = points.Count;
        while (remainingPoints > 0)
        {
            var nextIndex = (currentIndex + 1) % points.Count;
            var current = points[currentIndex].Position;
            var next = points[nextIndex];

            if (next.OnCurve)
            {
                edges.Add(new LineSegment(current, next.Position));
                currentIndex = nextIndex;
                remainingPoints--;
                continue;
            }

            var endIndex = (nextIndex + 1) % points.Count;
            var end = points[endIndex];
            edges.Add(new QuadraticSegment(current, next.Position, end.Position));
            currentIndex = endIndex;
            remainingPoints -= 2;
        }

        return new Contour(edges);
    }
}
