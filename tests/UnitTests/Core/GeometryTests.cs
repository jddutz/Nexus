using Nexus.Core;
using Nexus.Core.Geometry;
using Nexus.Core.Shapes;
using Silk.NET.Maths;
using MathSphere = Silk.NET.Maths.Sphere<float>;

namespace Tests;

public class GeometryTests
{
    [Fact]
    public void ClosestPointsClampToSegmentAndBoxBoundaries()
    {
        Assert.Equal(Vector3D<float>.UnitX, Distance.ClosestPoint(new Vector3D<float>(3, 2, 0), Vector3D<float>.Zero, Vector3D<float>.UnitX));
        Assert.Equal(5f, Distance.Squared(new Vector3D<float>(3, 2, 0), new Box3D<float>(Vector3D<float>.Zero, Vector3D<float>.One)));
        Assert.Equal(4f, Distance.Squared(new Vector3D<float>(2, 0, 0), Vector3D<float>.Zero, Vector3D<float>.Zero));
    }

    [Fact]
    public void SegmentDistanceHandlesCrossingParallelAndPointSegments()
    {
        Assert.Equal(0f, Distance.Squared(new Vector2D<float>(-1, 0), new Vector2D<float>(1, 0), new Vector2D<float>(0, -1), new Vector2D<float>(0, 1)));
        Assert.Equal(4f, Distance.Squared(new Vector2D<float>(0, 0), new Vector2D<float>(2, 0), new Vector2D<float>(1, 2), new Vector2D<float>(3, 2)));
        Assert.Equal(1f, Distance.Squared(Vector3D<float>.Zero, Vector3D<float>.Zero, Vector3D<float>.UnitX, Vector3D<float>.UnitX));
    }

    [Fact]
    public void SegmentBoxDistanceFindsInteriorMinimum()
    {
        var box = new Box3D<float>(-Vector3D<float>.One, Vector3D<float>.One);
        Assert.Equal(1f, Distance.Squared(new Vector3D<float>(-4, 2, 0), new Vector3D<float>(4, 2, 0), box));
        Assert.Equal(0f, Distance.Squared(new Vector3D<float>(-4, 0, 0), new Vector3D<float>(4, 0, 0), box));
        Assert.Equal(3f, Distance.Squared(new Vector3D<float>(2, 2, 2), new Vector3D<float>(2, 2, 2), box));
    }

    [Fact]
    public void CapsuleUsesTransformedEndpointsAndEnclosingRadius()
    {
        var capsule = new Capsule(Vector3D<float>.Zero, Vector3D<float>.UnitX, 0.5f)
        {
            Owner = new GameObject3D
            {
                Position = new Vector3D<float>(10, 20, 30),
                Scale = new Vector3D<float>(2, 3, 4),
                Quaternion = Quaternion<float>.CreateFromAxisAngle(Vector3D<float>.UnitZ, MathF.PI / 2),
            },
        };
        var snapshot = capsule.Bounds;
        Assert.Equal(new Vector3D<float>(10, 20, 30), snapshot.Start);
        Assert.InRange((snapshot.End - new Vector3D<float>(10, 22, 30)).Length, 0, 0.00001f);
        Assert.Equal(2f, snapshot.Radius);
        capsule.Owner = null;
        Assert.Equal(new Vector3D<float>(10, 20, 30), snapshot.Start);
    }

    [Fact]
    public void CapsuleComposesDistancesForSphereCapsuleAndBoxContact()
    {
        var capsule = new CapsuleGeometry(new Vector3D<float>(-2, 0, 0), new Vector3D<float>(2, 0, 0), 1);
        Assert.True(Intersection.Intersects(capsule, new MathSphere(new Vector3D<float>(0, 2, 0), 1)));
        Assert.False(Intersection.Intersects(capsule, new MathSphere(new Vector3D<float>(0, 2.1f, 0), 1)));
        Assert.True(Intersection.Intersects(capsule, new CapsuleGeometry(new Vector3D<float>(0, 2, -2), new Vector3D<float>(0, 2, 2), 1)));
        Assert.True(Intersection.Intersects(capsule, new Box3D<float>(new Vector3D<float>(-1, 1, -1), new Vector3D<float>(1, 2, 1))));
        Assert.False(Intersection.Intersects(capsule, new Box3D<float>(new Vector3D<float>(-1, 1.1f, -1), new Vector3D<float>(1, 2, 1))));
    }

    [Theory]
    [InlineData(-3, 0, 0, 2, 0, 0, true, 2)]
    [InlineData(0, 3, 0, 0, -2, 0, true, 1)]
    [InlineData(-3, 2, 0, 2, 0, 0, true, 3)]
    [InlineData(-3, 2.1f, 0, 2, 0, 0, false, 0)]
    [InlineData(0, 0, 0, 2, 0, 0, true, 0)]
    [InlineData(0, 0, 0, 0, 0, 0, false, 0)]
    public void CapsuleRayReturnsFirstWorldDistance(float x, float y, float z, float dx, float dy, float dz, bool hit, float expected)
    {
        var capsule = new CapsuleGeometry(new Vector3D<float>(0, -1, 0), new Vector3D<float>(0, 1, 0), 1);
        Assert.Equal(hit, Intersection.Intersects(capsule, new Ray3D<float>(new(x, y, z), new(dx, dy, dz)), out var distance));
        Assert.Equal(expected, distance, 5);
    }

    [Fact]
    public void ZeroLengthCapsuleBehavesAsSphere()
    {
        var capsule = new CapsuleGeometry(Vector3D<float>.Zero, Vector3D<float>.Zero, 1);
        Assert.True(Intersection.Intersects(capsule, new Ray3D<float>(new Vector3D<float>(-3, 0, 0), Vector3D<float>.UnitX), out var distance));
        Assert.Equal(2f, distance);
    }

    [Fact]
    public void CircleRayUsesItsPlaneAndSharedCoplanarCircleSection()
    {
        var disk = new CircleGeometry(new Silk.NET.Maths.Circle<float>(Vector2D<float>.Zero, 1), 5);
        Assert.True(Intersection.Intersects(disk, new Ray3D<float>(new Vector3D<float>(-3, 0, 5), new Vector3D<float>(2, 0, 0)), out var distance));
        Assert.Equal(2f, distance);
        Assert.False(Intersection.Intersects(disk, new Ray3D<float>(new Vector3D<float>(-3, 0, 6), Vector3D<float>.UnitX), out distance));
        Assert.Equal(0f, distance);
        Assert.False(Intersection.Intersects(disk, new Ray3D<float>(new Vector3D<float>(0, 0, 5), Vector3D<float>.Zero)));
    }

    [Fact]
    public void CapsuleBoxUsesWorldGeometryUnderShearAndDegenerateTransforms()
    {
        var transform = Matrix4X4<float>.Identity;
        transform.M21 = 2;
        var box = new OrientedBox(-Vector3D<float>.One, Vector3D<float>.One, transform);
        Assert.Equal(0f, Distance.Squared(new Vector3D<float>(-4, 0, 0), new Vector3D<float>(4, 0, 0), box));
        Assert.True(Intersection.Intersects(new CapsuleGeometry(new Vector3D<float>(-4, 2, 0), new Vector3D<float>(4, 2, 0), 1), box));
        Assert.False(Intersection.Intersects(new CapsuleGeometry(new Vector3D<float>(-4, 2.1f, 0), new Vector3D<float>(4, 2.1f, 0), 1), box));
        transform.M33 = 0;
        box = new OrientedBox(-Vector3D<float>.One, Vector3D<float>.One, transform);
        Assert.Equal(1f, Distance.Squared(new Vector3D<float>(-4, 0, 1), new Vector3D<float>(4, 0, 1), box), 5);
    }

    [Fact]
    public void AllPrimitivePairsAreSymmetricForContactAndSeparation()
    {
        IShape[] MakeShapes(float x) =>
        [
            new AxisAlignedBoundingBox(-Vector3D<float>.One,Vector3D<float>.One) { Owner = new GameObject3D { Position = new(x,0,0) } },
            new OrientedBoundingBox(-Vector3D<float>.One,Vector3D<float>.One) { Owner = new GameObject3D { Position = new(x,0,0) } },
            new Circle(new Vector2D<float>(x,0),1),
            new Sphere(new Vector3D<float>(x,0,0),1),
            new Capsule(new Vector3D<float>(x,-1,0),new Vector3D<float>(x,1,0),1),
        ];
        foreach (var first in MakeShapes(0))
            foreach (var (offset, expected) in new[] { (0f, true), (2f, true), (5f, false) })
                foreach (var second in MakeShapes(offset))
                {
                    Assert.Equal(expected, first.IntersectsWith(second));
                    Assert.Equal(expected, second.IntersectsWith(first));
                }
    }

    [Fact]
    public void OrientedShapeDispatchRejectsEnvelopeFalsePositivesAndZSeparation()
    {
        var box = new OrientedBoundingBox(new Vector3D<float>(-1, -0.1f, -0.1f), new Vector3D<float>(1, 0.1f, 0.1f))
        {
            Owner = new GameObject3D { Quaternion = Quaternion<float>.CreateFromAxisAngle(Vector3D<float>.UnitZ, MathF.PI / 4) },
        };
        IShape sphere = new Sphere(new Vector3D<float>(0, 0.8f, 0), 0.05f);
        Assert.False(box.IntersectsWith(sphere));
        Assert.False(sphere.IntersectsWith(box));
        IShape elevated = new AxisAlignedBoundingBox(new Vector3D<float>(-1, -1, 10), new Vector3D<float>(1, 1, 11));
        Assert.False(box.IntersectsWith(elevated));
        Assert.False(elevated.IntersectsWith(box));
    }

    [Fact]
    public void Explicit2DQueriesProjectCapsuleWhile3DQueriesRespectZ()
    {
        var capsule = new Capsule(new Vector3D<float>(-1, 0, 10), new Vector3D<float>(1, 0, 10), 1);
        Assert.True(capsule.IntersectsWith(new Box2D<float>(-1, -1, 1, 1)));
        Assert.False(capsule.IntersectsWith(new Box3D<float>(-Vector3D<float>.One, Vector3D<float>.One)));
        Assert.True(capsule.IntersectsWith(new Circle(Vector2D<float>.Zero, 1)));
    }

    [Fact]
    public void DegenerateOrientedBoxesStillSeparateInTheirAffinePlane()
    {
        var first = new OrientedBox(new Vector3D<float>(-1, -1, 0), new Vector3D<float>(1, 1, 0), Matrix4X4<float>.Identity);
        var second = new OrientedBox(new Vector3D<float>(3, -1, 0), new Vector3D<float>(5, 1, 0), Matrix4X4<float>.Identity);
        Assert.False(Intersection.Intersects(first, second));
        var point = new OrientedBox(Vector3D<float>.Zero, Vector3D<float>.Zero, Matrix4X4<float>.Identity);
        var otherPoint = new OrientedBox(Vector3D<float>.One, Vector3D<float>.One, Matrix4X4<float>.Identity);
        Assert.False(Intersection.Intersects(point, otherPoint));
        Assert.True(Intersection.Intersects(point, first));
        Assert.Throws<ArgumentNullException>(() => Intersection.Intersects((IShape)null!, new Sphere()));
    }
}
