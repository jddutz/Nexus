namespace Nexus.Core.Geometry;

/// <summary>A world-XY disk. Two-dimensional queries use its XY projection; rays use its Z plane.</summary>
public readonly record struct CircleGeometry(Circle<float> Bounds, float Z = 0f);

/// <summary>A box defined by local limits and an affine world transform, including shear.</summary>
public readonly record struct OrientedBox(
    Vector3D<float> Minimum,
    Vector3D<float> Maximum,
    Matrix4X4<float> Transform
)
{
    /// <summary>Gets the axis-aligned world envelope.</summary>
    public Box3D<float> Bounds => Intersection.OrientedBoundingBoxQueries.GetBounds(this);
}

/// <summary>A segment swept through a radius in world space.</summary>
public readonly record struct CapsuleGeometry(Vector3D<float> Start, Vector3D<float> End, float Radius);
