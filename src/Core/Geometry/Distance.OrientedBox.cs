namespace Nexus.Core.Geometry;

public static partial class Distance
{
    /// <summary>Minimum squared distance from a segment to an affine box, including degenerate boxes.</summary>
    public static float Squared(Vector3D<float> start, Vector3D<float> end, OrientedBox box)
    {
        if (Matrix4X4.Invert(box.Transform, out var inverse))
        {
            var p = Vector4D.Transform(new Vector4D<float>(start, 1), inverse);
            if (p.X >= box.Minimum.X && p.X <= box.Maximum.X && p.Y >= box.Minimum.Y && p.Y <= box.Maximum.Y && p.Z >= box.Minimum.Z && p.Z <= box.Maximum.Z) return 0;
        }
        var corners = Intersection.OrientedBoundingBoxQueries.GetWorldCorners(box);
        ReadOnlySpan<int> triangles = [0, 2, 6, 0, 6, 4, 1, 3, 7, 1, 7, 5, 0, 1, 5, 0, 5, 4, 2, 3, 7, 2, 7, 6, 0, 1, 3, 0, 3, 2, 4, 5, 7, 4, 7, 6];
        var result = float.PositiveInfinity;
        for (var i = 0; i < triangles.Length; i += 3)
            result = MathF.Min(result, SquaredToTriangle(start, end, corners[triangles[i]], corners[triangles[i + 1]], corners[triangles[i + 2]]));
        return result;
    }

    private static float SquaredToTriangle(Vector3D<float> start, Vector3D<float> end, Vector3D<float> a, Vector3D<float> b, Vector3D<float> c)
    {
        var edge1 = b - a; var edge2 = c - a; var direction = end - start;
        var p = Vector3D.Cross(direction, edge2);
        var determinant = Vector3D.Dot(edge1, p);
        if (determinant != 0)
        {
            var fromA = start - a;
            var u = Vector3D.Dot(fromA, p) / determinant;
            var q = Vector3D.Cross(fromA, edge1);
            var v = Vector3D.Dot(direction, q) / determinant;
            var t = Vector3D.Dot(edge2, q) / determinant;
            if (u >= 0 && v >= 0 && u + v <= 1 && t >= 0 && t <= 1) return 0;
        }
        return MathF.Min(
            MathF.Min(SquaredToTriangle(start, a, b, c), SquaredToTriangle(end, a, b, c)),
            MathF.Min(Squared(start, end, a, b), MathF.Min(Squared(start, end, b, c), Squared(start, end, c, a))));
    }
}
