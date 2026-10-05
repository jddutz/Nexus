using System.Numerics;

namespace Nexus.Assets.Typography.Geometry;

/// <summary>Coalesces adjacent quadratics within a cumulative source-space error budget.</summary>
public static class QuadraticContourSimplifier
{
    /// <summary>Preserves endpoints, order and lines. The budget excludes floating-point rounding.</summary>
    public static Contour Simplify(Contour contour, float tolerance)
    {
        ArgumentNullException.ThrowIfNull(contour);
        if (!float.IsFinite(tolerance) || tolerance < 0f)
            throw new ArgumentOutOfRangeException(nameof(tolerance));
        if (tolerance == 0f) return contour;
        var edges = contour.Edges.Select(edge => (Edge: edge, Error: 0f)).ToList();
        var changed = false;
        while (true)
        {
            var next = new List<(Edge Edge, float Error)>(edges.Count);
            var merged = false;
            for (var i = 0; i < edges.Count; i++)
            {
                if (i + 1 < edges.Count && edges[i].Edge is QuadraticSegment left
                    && edges[i + 1].Edge is QuadraticSegment right && left.End == right.Start)
                {
                    var control = 2f * left.End - .5f * (left.Start + right.End);
                    var firstControl = .5f * (left.Start + control);
                    var secondControl = .5f * (control + right.End);
                    var middle = .5f * (firstControl + secondControl);
                    // Comparing control polygons bounds the entire difference curve
                    // by their convex hull. Include previous errors for every merge.
                    var deviation = MathF.Max(Vector2.Distance(middle, left.End),
                        MathF.Max(Vector2.Distance(firstControl, left.Control),
                            Vector2.Distance(secondControl, right.Control)));
                    var error = MathF.Max(edges[i].Error, edges[i + 1].Error) + deviation;
                    if (error <= tolerance)
                    {
                        next.Add((new QuadraticSegment(left.Start, control, right.End), error));
                        i++;
                        merged = true;
                        continue;
                    }
                }
                next.Add(edges[i]);
            }
            if (!merged) break;
            changed = true;
            edges = next;
        }
        return changed ? new Contour(edges.Select(entry => entry.Edge)) : contour;
    }
}
