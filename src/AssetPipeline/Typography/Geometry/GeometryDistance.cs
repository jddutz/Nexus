using System.Numerics;

namespace Nexus.AssetPipeline.Typography.Geometry;

/// <summary>
/// Calculates signed distances to geometry edges and closed shapes.
/// </summary>
public static class GeometryDistance
{
    /// <summary>
    /// Calculates the signed distance to a directed line or quadratic edge. The left side of the
    /// directed edge is positive and the right side is negative.
    /// </summary>
    /// <param name="point">The point to measure from.</param>
    /// <param name="edge">The edge to measure to.</param>
    /// <returns>The signed shortest distance to the edge.</returns>
    public static float SignedDistanceToEdge(Vector2 point, Edge edge)
    {
        ArgumentNullException.ThrowIfNull(edge);

        var (closestPoint, parameter) = FindClosestPoint(point, edge);
        var tangent = GetTangent(edge, parameter);
        if (tangent.LengthSquared() <= 1e-20f)
            tangent = edge.End - edge.Start;

        var offset = point - closestPoint;
        var distance = offset.Length();
        if (distance == 0f)
            return 0f;

        var cross = (double)tangent.X * offset.Y - (double)tangent.Y * offset.X;
        return MathF.CopySign(distance, (float)cross);
    }

    /// <summary>
    /// Calculates a signed distance to a closed contour, negative inside and positive outside.
    /// </summary>
    /// <param name="point">The point to measure from.</param>
    /// <param name="contour">The closed contour to measure to.</param>
    /// <returns>The signed shortest distance to the contour.</returns>
    public static float SignedDistanceToContour(Vector2 point, Contour contour)
    {
        ArgumentNullException.ThrowIfNull(contour);
        return SignedDistanceToShape(point, [contour]);
    }

    /// <summary>
    /// Calculates a signed distance to closed contours, negative inside and positive outside.
    /// Contours use the nonzero winding fill rule, so oppositely oriented contours form holes.
    /// </summary>
    /// <param name="point">The point to measure from.</param>
    /// <param name="contours">The closed contours that form the shape.</param>
    /// <returns>The signed shortest distance to the shape boundary.</returns>
    public static float SignedDistanceToShape(Vector2 point, IEnumerable<Contour> contours)
    {
        ArgumentNullException.ThrowIfNull(contours);

        var windingNumber = 0;
        var minimumDistanceSquared = double.PositiveInfinity;
        foreach (var contour in contours)
        {
            ArgumentNullException.ThrowIfNull(contour);
            windingNumber += GetWindingNumber(point, contour);
            foreach (var edge in contour.Edges)
            {
                var (closestPoint, _) = FindClosestPoint(point, edge);
                var dx = (double)point.X - closestPoint.X;
                var dy = (double)point.Y - closestPoint.Y;
                minimumDistanceSquared = Math.Min(minimumDistanceSquared, dx * dx + dy * dy);
            }
        }

        if (double.IsPositiveInfinity(minimumDistanceSquared))
            return float.PositiveInfinity;

        var distance = (float)Math.Sqrt(minimumDistanceSquared);
        if (distance == 0f)
            return 0f;

        return windingNumber == 0 ? distance : -distance;
    }

    /// <summary>
    /// Finds the closest point on an edge and its curve parameter.
    /// </summary>
    /// <param name="point">The query point.</param>
    /// <param name="edge">The edge to search.</param>
    /// <returns>The closest edge point and its parameter.</returns>
    private static (Vector2 Point, double Parameter) FindClosestPoint(Vector2 point, Edge edge)
    {
        if (edge is LineSegment)
        {
            var startX = (double)edge.Start.X;
            var startY = (double)edge.Start.Y;
            var dx = (double)edge.End.X - startX;
            var dy = (double)edge.End.Y - startY;
            var lengthSquared = dx * dx + dy * dy;
            var parameter =
                lengthSquared == 0d
                    ? 0d
                    : Math.Clamp(
                        ((point.X - startX) * dx + (point.Y - startY) * dy) / lengthSquared,
                        0d,
                        1d
                    );
            return (EvaluateLine(edge.Start, edge.End, parameter), parameter);
        }

        if (edge is not QuadraticSegment quadratic)
            throw new NotSupportedException($"Unsupported edge type: {edge.GetType().FullName}.");

        var ax = (double)quadratic.Start.X - 2d * quadratic.Control.X + quadratic.End.X;
        var ay = (double)quadratic.Start.Y - 2d * quadratic.Control.Y + quadratic.End.Y;
        var bx = 2d * ((double)quadratic.Control.X - quadratic.Start.X);
        var by = 2d * ((double)quadratic.Control.Y - quadratic.Start.Y);
        var cx = (double)quadratic.Start.X - point.X;
        var cy = (double)quadratic.Start.Y - point.Y;

        var coefficients = new[]
        {
            2d * (ax * ax + ay * ay),
            3d * (ax * bx + ay * by),
            bx * bx + by * by + 2d * (ax * cx + ay * cy),
            bx * cx + by * cy,
        };
        var candidates = new List<double> { 0d, 1d };
        candidates.AddRange(FindCubicRootsInUnitInterval(coefficients));

        var bestParameter = 0d;
        var bestDistanceSquared = double.PositiveInfinity;
        foreach (var candidate in candidates)
        {
            var candidatePoint = EvaluateQuadratic(quadratic, candidate);
            var dx = (double)point.X - candidatePoint.X;
            var dy = (double)point.Y - candidatePoint.Y;
            var distanceSquared = dx * dx + dy * dy;
            if (distanceSquared < bestDistanceSquared)
            {
                bestDistanceSquared = distanceSquared;
                bestParameter = candidate;
            }
        }

        return (EvaluateQuadratic(quadratic, bestParameter), bestParameter);
    }

    /// <summary>
    /// Evaluates a line segment at a parameter in the unit interval.
    /// </summary>
    /// <param name="start">The segment start.</param>
    /// <param name="end">The segment end.</param>
    /// <param name="parameter">The segment parameter.</param>
    /// <returns>The point at the parameter.</returns>
    private static Vector2 EvaluateLine(Vector2 start, Vector2 end, double parameter) =>
        new(
            (float)(start.X + (end.X - start.X) * parameter),
            (float)(start.Y + (end.Y - start.Y) * parameter)
        );

    /// <summary>
    /// Evaluates a quadratic edge at a parameter in the unit interval.
    /// </summary>
    /// <param name="edge">The quadratic edge.</param>
    /// <param name="parameter">The curve parameter.</param>
    /// <returns>The point at the parameter.</returns>
    private static Vector2 EvaluateQuadratic(QuadraticSegment edge, double parameter)
    {
        var inverse = 1d - parameter;
        var startWeight = inverse * inverse;
        var controlWeight = 2d * inverse * parameter;
        var endWeight = parameter * parameter;
        return new Vector2(
            (float)(
                startWeight * edge.Start.X + controlWeight * edge.Control.X + endWeight * edge.End.X
            ),
            (float)(
                startWeight * edge.Start.Y + controlWeight * edge.Control.Y + endWeight * edge.End.Y
            )
        );
    }

    /// <summary>
    /// Gets an edge's tangent at the specified parameter.
    /// </summary>
    /// <param name="edge">The edge to evaluate.</param>
    /// <param name="parameter">The curve parameter.</param>
    /// <returns>The tangent vector.</returns>
    private static Vector2 GetTangent(Edge edge, double parameter)
    {
        if (edge is LineSegment)
            return edge.End - edge.Start;

        if (edge is QuadraticSegment quadratic)
        {
            return new Vector2(
                (float)(
                    2d
                    * (
                        (1d - parameter) * (quadratic.Control.X - quadratic.Start.X)
                        + parameter * (quadratic.End.X - quadratic.Control.X)
                    )
                ),
                (float)(
                    2d
                    * (
                        (1d - parameter) * (quadratic.Control.Y - quadratic.Start.Y)
                        + parameter * (quadratic.End.Y - quadratic.Control.Y)
                    )
                )
            );
        }

        throw new NotSupportedException($"Unsupported edge type: {edge.GetType().FullName}.");
    }

    /// <summary>
    /// Computes the contour's signed ray-crossing count at a point.
    /// </summary>
    /// <param name="point">The query point.</param>
    /// <param name="contour">The contour to classify.</param>
    /// <returns>The signed winding number.</returns>
    private static int GetWindingNumber(Vector2 point, Contour contour)
    {
        var windingNumber = 0;
        foreach (var edge in contour.Edges)
        {
            if (edge is LineSegment)
            {
                windingNumber += GetLineCrossing(point, edge.Start, edge.End);
            }
            else if (edge is QuadraticSegment quadratic)
            {
                windingNumber += GetQuadraticCrossings(point, quadratic);
            }
            else
            {
                throw new NotSupportedException(
                    $"Unsupported edge type: {edge.GetType().FullName}."
                );
            }
        }

        return windingNumber;
    }

    /// <summary>
    /// Counts a directed line crossing of the ray extending right from a point.
    /// </summary>
    /// <param name="point">The ray origin.</param>
    /// <param name="start">The line start.</param>
    /// <param name="end">The line end.</param>
    /// <returns>One for an upward crossing, negative one for a downward crossing, or zero.</returns>
    private static int GetLineCrossing(Vector2 point, Vector2 start, Vector2 end)
    {
        if (start.Y <= point.Y && end.Y > point.Y && IsRightOfRay(point, start, end))
            return 1;
        if (start.Y > point.Y && end.Y <= point.Y && IsRightOfRay(point, start, end))
            return -1;
        return 0;
    }

    /// <summary>
    /// Determines whether a directed line intersects the ray to the right of its origin.
    /// </summary>
    /// <param name="point">The ray origin.</param>
    /// <param name="start">The line start.</param>
    /// <param name="end">The line end.</param>
    /// <returns>True when the line crosses the ray to its right.</returns>
    private static bool IsRightOfRay(Vector2 point, Vector2 start, Vector2 end)
    {
        var cross =
            ((double)end.X - start.X) * (point.Y - start.Y)
            - ((double)end.Y - start.Y) * (point.X - start.X);
        return end.Y > start.Y ? cross > 0d : cross < 0d;
    }

    /// <summary>
    /// Counts directed ray crossings of a quadratic edge.
    /// </summary>
    /// <param name="point">The ray origin.</param>
    /// <param name="edge">The quadratic edge.</param>
    /// <returns>The signed number of crossings.</returns>
    private static int GetQuadraticCrossings(Vector2 point, QuadraticSegment edge)
    {
        var a = (double)edge.Start.Y - 2d * edge.Control.Y + edge.End.Y;
        var b = 2d * ((double)edge.Control.Y - edge.Start.Y);
        var c = (double)edge.Start.Y - point.Y;
        var crossings = 0;

        foreach (var parameter in SolveQuadratic(a, b, c))
        {
            if (parameter < 0d || parameter > 1d)
                continue;

            var inverse = 1d - parameter;
            var x =
                inverse * inverse * edge.Start.X
                + 2d * inverse * parameter * edge.Control.X
                + parameter * parameter * edge.End.X;
            if (x <= point.X)
                continue;

            var derivative = 2d * a * parameter + b;
            var direction = GetCrossingDirection(a, derivative, parameter);
            if (direction > 0 && parameter < 1d)
                crossings++;
            else if (direction < 0 && parameter > 0d)
                crossings--;
        }

        return crossings;
    }

    /// <summary>
    /// Determines the vertical direction of a quadratic crossing, including endpoint roots.
    /// </summary>
    /// <param name="quadraticCoefficient">The quadratic coefficient of the y polynomial.</param>
    /// <param name="derivative">The y derivative at the root.</param>
    /// <param name="parameter">The root parameter.</param>
    /// <returns>One for upward, negative one for downward, or zero for a tangent.</returns>
    private static int GetCrossingDirection(
        double quadraticCoefficient,
        double derivative,
        double parameter
    )
    {
        if (derivative > 1e-12d)
            return 1;
        if (derivative < -1e-12d)
            return -1;
        if (parameter == 0d)
            return Math.Sign(quadraticCoefficient);
        if (parameter == 1d)
            return -Math.Sign(quadraticCoefficient);
        return 0;
    }

    /// <summary>
    /// Finds the real roots of a cubic polynomial that lie in the unit interval.
    /// </summary>
    /// <param name="coefficients">Cubic coefficients ordered from degree three to zero.</param>
    /// <returns>The distinct roots in the unit interval.</returns>
    private static IEnumerable<double> FindCubicRootsInUnitInterval(double[] coefficients)
    {
        var scale = coefficients.Max(Math.Abs);
        if (scale == 0d)
            return [];

        var tolerance = scale * 1e-12d;
        var criticalPoints = SolveQuadratic(
                3d * coefficients[0],
                2d * coefficients[1],
                coefficients[2]
            )
            .Where(value => value > 0d && value < 1d);
        var boundaries = new[] { 0d }
            .Concat(criticalPoints)
            .Append(1d)
            .Distinct()
            .Order()
            .ToArray();
        var roots = new List<double>();

        for (var index = 0; index < boundaries.Length; index++)
        {
            var value = EvaluateCubic(coefficients, boundaries[index]);
            if (Math.Abs(value) <= tolerance)
                AddDistinctRoot(roots, boundaries[index]);

            if (index + 1 >= boundaries.Length)
                continue;

            var left = boundaries[index];
            var right = boundaries[index + 1];
            var leftValue = value;
            var rightValue = EvaluateCubic(coefficients, right);
            if (
                leftValue == 0d
                || rightValue == 0d
                || Math.Sign(leftValue) == Math.Sign(rightValue)
            )
                continue;

            for (var iteration = 0; iteration < 64; iteration++)
            {
                var middle = (left + right) / 2d;
                var middleValue = EvaluateCubic(coefficients, middle);
                if (Math.Abs(middleValue) <= tolerance)
                {
                    left = middle;
                    right = middle;
                    break;
                }

                if (Math.Sign(leftValue) == Math.Sign(middleValue))
                {
                    left = middle;
                    leftValue = middleValue;
                }
                else
                {
                    right = middle;
                }
            }

            AddDistinctRoot(roots, (left + right) / 2d);
        }

        return roots;
    }

    /// <summary>
    /// Evaluates a cubic polynomial using Horner's method.
    /// </summary>
    /// <param name="coefficients">Cubic coefficients ordered from degree three to zero.</param>
    /// <param name="parameter">The polynomial input.</param>
    /// <returns>The polynomial value.</returns>
    private static double EvaluateCubic(double[] coefficients, double parameter) =>
        ((coefficients[0] * parameter + coefficients[1]) * parameter + coefficients[2]) * parameter
        + coefficients[3];

    /// <summary>
    /// Solves a quadratic equation, including its linear and constant degeneracies.
    /// </summary>
    /// <param name="a">The quadratic coefficient.</param>
    /// <param name="b">The linear coefficient.</param>
    /// <param name="c">The constant coefficient.</param>
    /// <returns>The real roots.</returns>
    private static IEnumerable<double> SolveQuadratic(double a, double b, double c)
    {
        if (a == 0d)
        {
            if (b == 0d)
                return [];
            return [-c / b];
        }

        var discriminant = b * b - 4d * a * c;
        if (discriminant < 0d)
            return [];
        if (discriminant == 0d)
            return [-b / (2d * a)];

        var squareRoot = Math.Sqrt(discriminant);
        var q = -0.5d * (b + Math.CopySign(squareRoot, b));
        return q == 0d ? [-b / (2d * a)] : [q / a, c / q];
    }

    /// <summary>
    /// Adds a root unless an equivalent root is already present.
    /// </summary>
    /// <param name="roots">The roots collected so far.</param>
    /// <param name="root">The root to add.</param>
    private static void AddDistinctRoot(List<double> roots, double root)
    {
        if (roots.All(existing => Math.Abs(existing - root) > 1e-10d))
            roots.Add(root);
    }
}
