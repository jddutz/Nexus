using Nexus.Core;
using Nexus.Core.Geometry;
using Nexus.Core.Shapes;
using Silk.NET.Maths;
using MathCircle = Silk.NET.Maths.Circle<float>;
using MathSphere = Silk.NET.Maths.Sphere<float>;

namespace Tests;

public class GeometryValidationTests
{
    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(1, 1, 0)]
    [InlineData(1, 0, 0)]
    [InlineData(0, 0, 0)]
    public void ContainedSegmentsHaveZeroDistanceForSolidFlatLineAndPointBoxes(float x, float y, float z)
    {
        var extent = new Vector3D<float>(x, y, z);
        foreach (var transform in AffineTransforms())
        {
            var box = new OrientedBox(-extent, extent, transform);
            var start = Transform(-extent * 0.5f, transform);
            var end = Transform(extent * 0.5f, transform);
            Assert.Equal(0f, Distance.Squared(start, end, box), 5);
            Assert.Equal(0f, Distance.Squared(end, start, box), 5);
            Assert.True(Intersection.Intersects(new CapsuleGeometry(start, end, 0), box));
        }
    }

    [Theory]
    [InlineData(1, 1, 0)]
    [InlineData(1, 0, 0)]
    [InlineData(0, 0, 0)]
    public void CollapsedTransformsPreserveContainedSegmentDistance(float x, float y, float z)
    {
        var transform = Matrix4X4.CreateScale(x, y, z);
        transform.M21 = y;
        transform.M41 = 4;
        transform.M42 = -2;
        var box = new OrientedBox(-Vector3D<float>.One, Vector3D<float>.One, transform);
        var start = Transform(new Vector3D<float>(-0.5f, 0.25f, 0.5f), transform);
        var end = Transform(new Vector3D<float>(0.25f, -0.5f, -0.25f), transform);
        Assert.Equal(0f, Distance.Squared(start, end, box), 5);
        Assert.Equal(0f, Distance.Squared(end, start, box), 5);
        Assert.True(Intersection.Intersects(new CapsuleGeometry(start, end, 0), box));
    }

    [Theory]
    // Coplanar rectangles: overlap, edge contact, corner contact, separation.
    [InlineData(1, 1, 0, 1, 1, 0, 0, 0, 0, true)]
    [InlineData(1, 1, 0, 1, 1, 0, 2, 0, 0, true)]
    [InlineData(1, 1, 0, 1, 1, 0, 2, 2, 0, true)]
    [InlineData(1, 1, 0, 1, 1, 0, 3, 0, 0, false)]
    [InlineData(1, 1, 0, 1, 1, 0, 0, 0, 1, false)]
    // Crossing, touching, parallel, skew, and disjoint collinear segments.
    [InlineData(1, 0, 0, 0, 1, 0, 0, 0, 0, true)]
    [InlineData(1, 0, 0, 0, 1, 0, 1, 1, 0, true)]
    [InlineData(1, 0, 0, 1, 0, 0, 0, 1, 0, false)]
    [InlineData(1, 0, 0, 0, 1, 0, 0, 0, 1, false)]
    [InlineData(1, 0, 0, 1, 0, 0, 3, 0, 0, false)]
    // Points and segments against flat boxes.
    [InlineData(1, 1, 0, 0, 0, 0, 1, 1, 0, true)]
    [InlineData(1, 1, 0, 0, 0, 0, 2, 0, 0, false)]
    [InlineData(1, 1, 0, 0, 0, 1, 0, 0, 0, true)]
    [InlineData(1, 1, 0, 0, 0, 1, 2, 0, 0, false)]
    public void DegenerateBoxPairsPreserveExpectedIntersectionUnderCommonAffineTransforms(
        float ax, float ay, float az, float bx, float by, float bz,
        float x, float y, float z, bool expected)
    {
        var firstExtent = new Vector3D<float>(ax, ay, az);
        var secondExtent = new Vector3D<float>(bx, by, bz);
        var offset = new Vector3D<float>(x, y, z);
        foreach (var transform in AffineTransforms())
        {
            var first = new OrientedBox(-firstExtent, firstExtent, transform);
            var second = new OrientedBox(offset - secondExtent, offset + secondExtent, transform);
            Assert.Equal(expected, Intersection.Intersects(first, second));
            Assert.Equal(expected, Intersection.Intersects(second, first));
        }
    }

    [Fact]
    public void BoxPairsWithDifferentOrientationsHandleCrossingAndSeparatedPlanes()
    {
        var flat = new OrientedBox(new(-1, -1, 0), new(1, 1, 0), Matrix4X4<float>.Identity);
        var turn = Matrix4X4<float>.Identity;
        turn.M22 = 0; turn.M23 = 1; turn.M32 = -1; turn.M33 = 0;
        var upright = new OrientedBox(new(-1, -1, 0), new(1, 1, 0), turn);
        Assert.True(Intersection.Intersects(flat, upright));
        Assert.True(Intersection.Intersects(upright, flat));
        turn.M41 = 3;
        upright = upright with { Transform = turn };
        Assert.False(Intersection.Intersects(flat, upright));
        Assert.False(Intersection.Intersects(upright, flat));
    }

    [Fact]
    public void AllFifteenPairsRemainSymmetricWithNonuniformlyScaledRotatedOwners()
    {
        for (var firstIndex = 0; firstIndex < 5; firstIndex++)
            for (var secondIndex = firstIndex; secondIndex < 5; secondIndex++)
                foreach (var (offset, expected) in new[] { (0f, true), (20f, false) })
                {
                    var first = Shapes(new GameObject3D
                    {
                        Scale = new(2, 1, 0.5f),
                        Quaternion = Quaternion<float>.CreateFromAxisAngle(Vector3D<float>.UnitZ, 0.6f),
                    })[firstIndex];
                    var second = Shapes(new GameObject3D
                    {
                        Position = new(offset, 0, 0),
                        Scale = new(0.5f, 2, 1),
                        Quaternion = Quaternion<float>.CreateFromAxisAngle(Vector3D<float>.UnitY, 0.4f),
                    })[secondIndex];
                    Assert.Equal(expected, first.IntersectsWith(second));
                    Assert.Equal(expected, second.IntersectsWith(first));
                }
    }

    [Fact]
    public void AllPrimitivesReportWorldRayDistancesAndInteriorHits()
    {
        foreach (var shape in Shapes(new GameObject3D { Position = new(10, 20, 30) }))
        {
            Assert.True(shape.IntersectsWith(new Ray3D<float>(new(5, 20, 30), new(2, 0, 0)), out var distance));
            Assert.Equal(4f, distance, 5);
            Assert.True(shape.IntersectsWith(new Ray3D<float>(new(10, 20, 30), new(2, 0, 0)), out distance));
            Assert.Equal(0f, distance);
            Assert.False(shape.IntersectsWith(new Ray3D<float>(new(5, 20, 30), new(-2, 0, 0)), out distance));
            Assert.Equal(0f, distance);
            Assert.False(shape.IntersectsWith(new Ray3D<float>(new(10, 20, 30), Vector3D<float>.Zero), out distance));
            Assert.Equal(0f, distance);
        }
    }

    [Fact]
    public void AllPrimitivesProject2DQueriesButRespectDepthIn3DQueries()
    {
        foreach (var shape in Shapes(new GameObject3D { Position = new(0, 0, 10) }))
        {
            Assert.True(shape.IntersectsWith(new Box2D<float>(-1, -1, 1, 1)));
            Assert.True(shape.IntersectsWith(new Rectangle<float>(-1, -1, 2, 2)));
            Assert.True(shape.IntersectsWith(new MathCircle(Vector2D<float>.Zero, 1)));
            // A Circle intentionally projects even when the other input is 3D.
            Assert.Equal(shape is Circle, shape.IntersectsWith(new Box3D<float>(-Vector3D<float>.One, Vector3D<float>.One)));
            Assert.Equal(shape is Circle, shape.IntersectsWith(new MathSphere(Vector3D<float>.Zero, 1)));
        }
    }

    [Fact]
    public void ShearedBoxRayDistanceUsesWorldUnitsAndRejectsEnvelopeOnlyHits()
    {
        var transform = Matrix4X4<float>.Identity;
        transform.M11 = 2; transform.M21 = 1; transform.M22 = 3;
        var box = new OrientedBox(-Vector3D<float>.One, Vector3D<float>.One, transform);
        Assert.True(Intersection.Intersects(box, new Ray3D<float>(new(-5, 0, 0), new(4, 0, 0)), out var distance));
        Assert.Equal(3f, distance, 5);
        Assert.False(Intersection.Intersects(box, new Ray3D<float>(new(-2.5f, 2, 5), new(0, 0, -2)), out distance));
        Assert.Equal(0f, distance);
    }

    [Fact]
    public void ParentScaleAndChildRotationUseMaximumStretchForRoundPrimitives()
    {
        var parent = new GameObject3D { Scale = new(4, 1, 2) };
        var owner = new GameObject3D
        {
            Quaternion = Quaternion<float>.CreateFromAxisAngle(Vector3D<float>.UnitZ, MathF.PI / 4),
        };
        parent.AddChild(owner);
        var transform = owner.WorldTransform;
        var firstBasis = new Vector3D<float>(transform.M11, transform.M12, transform.M13);
        var secondBasis = new Vector3D<float>(transform.M21, transform.M22, transform.M23);
        Assert.True(MathF.Abs(Vector3D.Dot(firstBasis, secondBasis)) > 1);

        var circle = new Circle(Vector2D<float>.Zero, 0.5f) { Owner = owner };
        var sphere = new Sphere(Vector3D<float>.Zero, 0.5f) { Owner = owner };
        var capsule = new Capsule(Vector3D<float>.Zero, Vector3D<float>.UnitX, 0.5f) { Owner = owner };
        Assert.Equal(2f, circle.Bounds.Radius, 5);
        Assert.Equal(2f, sphere.Bounds.Radius, 5);
        Assert.Equal(2f, capsule.Bounds.Radius, 5);
        Assert.InRange((capsule.Bounds.End - new Vector3D<float>(4 * MathF.Sqrt(0.5f), MathF.Sqrt(0.5f), 0)).Length, 0, 0.00001f);
    }

    [Fact]
    public void UnsupportedComponentsThrowWithoutInvokingTheirQueries()
    {
        IShape unknown = new UnsupportedShape();
        foreach (var primitive in Shapes(new GameObject3D()))
        {
            Assert.Throws<NotSupportedException>(() => primitive.IntersectsWith(unknown));
            Assert.Throws<NotSupportedException>(() => Intersection.Intersects(unknown, primitive));
        }
    }

    private static IShape[] Shapes(GameObject3D owner) =>
    [
        new AxisAlignedBoundingBox(-Vector3D<float>.One, Vector3D<float>.One) { Owner = owner },
        new OrientedBoundingBox(-Vector3D<float>.One, Vector3D<float>.One) { Owner = owner },
        new Circle(Vector2D<float>.Zero, 1) { Owner = owner },
        new Sphere(Vector3D<float>.Zero, 1) { Owner = owner },
        new Capsule(new Vector3D<float>(0, -1, 0), new Vector3D<float>(0, 1, 0), 1) { Owner = owner },
    ];

    private static IEnumerable<Matrix4X4<float>> AffineTransforms()
    {
        yield return Matrix4X4<float>.Identity;
        var transform = Matrix4X4<float>.Identity;
        transform.M11 = 2; transform.M22 = 3; transform.M33 = 0.5f;
        transform.M21 = 1; transform.M31 = 0.5f;
        transform.M41 = 4; transform.M42 = -2; transform.M43 = 8;
        yield return transform;
        transform.M11 = -2;
        yield return transform;
    }

    private static Vector3D<float> Transform(Vector3D<float> point, Matrix4X4<float> matrix)
    {
        var p = Vector4D.Transform(new Vector4D<float>(point, 1), matrix);
        return new(p.X, p.Y, p.Z);
    }

    private sealed class UnsupportedShape : Component, IShape
    {
        public bool IntersectsWith(IShape other) => throw new InvalidOperationException();
        public bool IntersectsWith(Ray3D<float> ray) => throw new InvalidOperationException();
        public bool IntersectsWith(Ray3D<float> ray, out float distance) => throw new InvalidOperationException();
        public bool IntersectsWith(Box2D<float> box) => throw new InvalidOperationException();
        public bool IntersectsWith(Box3D<float> box) => throw new InvalidOperationException();
        public bool IntersectsWith(Rectangle<float> rect) => throw new InvalidOperationException();
        public bool IntersectsWith(MathCircle circle) => throw new InvalidOperationException();
        public bool IntersectsWith(MathSphere sphere) => throw new InvalidOperationException();
    }
}
