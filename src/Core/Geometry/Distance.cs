namespace Nexus.Core.Geometry;

/// <summary>Closest-point and squared-distance operations on world-space geometry.</summary>
public static partial class Distance
{
    public static Vector3D<float> ClosestPoint(Vector3D<float> point, Vector3D<float> start, Vector3D<float> end)
    {
        var edge = end - start;
        var t = edge.LengthSquared == 0f ? 0f : Math.Clamp(Vector3D.Dot(point - start, edge) / edge.LengthSquared, 0f, 1f);
        return start + edge * t;
    }

    public static Vector2D<float> ClosestPoint(Vector2D<float> point, Vector2D<float> start, Vector2D<float> end)
    {
        var edge = end - start;
        var t = edge.LengthSquared == 0f ? 0f : Math.Clamp(Vector2D.Dot(point - start, edge) / edge.LengthSquared, 0f, 1f);
        return start + edge * t;
    }

    public static Vector3D<float> ClosestPoint(Vector3D<float> point, Box3D<float> box) =>
        new(Math.Clamp(point.X, box.Min.X, box.Max.X), Math.Clamp(point.Y, box.Min.Y, box.Max.Y), Math.Clamp(point.Z, box.Min.Z, box.Max.Z));

    public static Vector2D<float> ClosestPoint(Vector2D<float> point, Box2D<float> box) =>
        new(Math.Clamp(point.X, box.Min.X, box.Max.X), Math.Clamp(point.Y, box.Min.Y, box.Max.Y));

    public static float Squared(Vector3D<float> point, Vector3D<float> start, Vector3D<float> end) => (point - ClosestPoint(point, start, end)).LengthSquared;
    public static float Squared(Vector2D<float> point, Vector2D<float> start, Vector2D<float> end) => (point - ClosestPoint(point, start, end)).LengthSquared;
    public static float Squared(Vector3D<float> point, Box3D<float> box) => (point - ClosestPoint(point, box)).LengthSquared;
    public static float Squared(Vector2D<float> point, Box2D<float> box) => (point - ClosestPoint(point, box)).LengthSquared;

    /// <summary>Minimum squared distance between two finite segments, including point segments.</summary>
    public static float Squared(Vector3D<float> a, Vector3D<float> b, Vector3D<float> c, Vector3D<float> d)
    {
        var u = b - a; var v = d - c; var w = a - c;
        // Double intermediates avoid cancellation for nearly parallel segments.
        double uu = Dot(u, u), vv = Dot(v, v), uv = Dot(u, v);
        double uw = Dot(u, w), vw = Dot(v, w);
        if (uu == 0) return Squared(a, c, d);
        if (vv == 0) return Squared(c, a, b);
        var denominator = uu * vv - uv * uv;
        var s = denominator > 0 ? Math.Clamp((uv * vw - uw * vv) / denominator, 0, 1) : 0;
        var t = (uv * s + vw) / vv;
        if (t < 0) { t = 0; s = Math.Clamp(-uw / uu, 0, 1); }
        else if (t > 1) { t = 1; s = Math.Clamp((uv - uw) / uu, 0, 1); }
        return (a + u * (float)s - c - v * (float)t).LengthSquared;
    }

    private static double Dot(Vector3D<float> a, Vector3D<float> b) =>
        (double)a.X * b.X + (double)a.Y * b.Y + (double)a.Z * b.Z;

    public static float Squared(Vector2D<float> a, Vector2D<float> b, Vector2D<float> c, Vector2D<float> d) =>
        Squared(new Vector3D<float>(a, 0), new Vector3D<float>(b, 0), new Vector3D<float>(c, 0), new Vector3D<float>(d, 0));

    /// <summary>Exact piecewise-quadratic minimization of segment-to-box distance.</summary>
    public static float Squared(Vector3D<float> start, Vector3D<float> end, Box3D<float> box)
    {
        var delta = end - start;
        Span<float> breaks = stackalloc float[8];
        breaks[0] = 0; breaks[1] = 1; var count = 2;
        AddBreaks(start.X, delta.X, box.Min.X, box.Max.X, breaks, ref count);
        AddBreaks(start.Y, delta.Y, box.Min.Y, box.Max.Y, breaks, ref count);
        AddBreaks(start.Z, delta.Z, box.Min.Z, box.Max.Z, breaks, ref count);
        breaks = breaks[..count]; breaks.Sort();
        var result = float.PositiveInfinity;
        for (var i = 0; i < count - 1; i++)
        {
            var lo = breaks[i]; var hi = breaks[i + 1]; var mid = (lo + hi) * 0.5f;
            float quadratic = 0, linear = 0;
            Accumulate(start.X, delta.X, box.Min.X, box.Max.X, mid, ref quadratic, ref linear);
            Accumulate(start.Y, delta.Y, box.Min.Y, box.Max.Y, mid, ref quadratic, ref linear);
            Accumulate(start.Z, delta.Z, box.Min.Z, box.Max.Z, mid, ref quadratic, ref linear);
            var t = quadratic == 0 ? lo : Math.Clamp(-linear / quadratic, lo, hi);
            result = MathF.Min(result, Squared(start + delta * t, box));
        }
        return result;
    }

    public static float Squared(Vector2D<float> start, Vector2D<float> end, Box2D<float> box) =>
        Squared(new Vector3D<float>(start, 0), new Vector3D<float>(end, 0), new Box3D<float>(new Vector3D<float>(box.Min, 0), new Vector3D<float>(box.Max, 0)));

    private static void AddBreaks(float origin, float direction, float min, float max, Span<float> values, ref int count)
    {
        if (direction == 0) return;
        var a = (min - origin) / direction; var b = (max - origin) / direction;
        if (a > 0 && a < 1) values[count++] = a;
        if (b > 0 && b < 1) values[count++] = b;
    }

    private static void Accumulate(float origin, float direction, float min, float max, float t, ref float quadratic, ref float linear)
    {
        var p = origin + direction * t;
        if (p >= min && p <= max) return;
        var offset = origin - (p < min ? min : max);
        quadratic += direction * direction; linear += direction * offset;
    }
}
