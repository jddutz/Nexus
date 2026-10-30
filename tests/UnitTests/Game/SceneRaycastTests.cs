using Nexus.Core;
using Nexus.GameEngine;
using Silk.NET.Maths;
using SphereShape = Nexus.Core.Shapes.Sphere;

namespace Tests;

public class SceneRaycastTests
{
    [Fact]
    public void Raycast_returnsNearestHitPerOwnerOrderedByWorldDistance()
    {
        IScene scene = new Scene { MainCamera = new Nexus.Graphics.Cameras.StaticCamera() };
        var far = new GameObject();
        far.AddComponent(new SphereShape(new(10, 0, 0), 1));
        var near = new GameObject();
        near.AddComponent(new SphereShape(new(6, 0, 0), 1));
        near.AddComponent(new SphereShape(new(3, 0, 0), 1));
        var miss = new GameObject();
        miss.AddComponent(new SphereShape(new(0, 5, 0), 1));
        far.Children.Add(near);
        scene.Children.Add(far);
        scene.Children.Add(miss);

        var hits = scene.Raycast(new(Vector3D<float>.Zero, new(2, 0, 0)));

        Assert.Equal(2, hits.Count);
        Assert.Equal(new RaycastHit(near, 2), hits[0]);
        Assert.Equal(new RaycastHit(far, 9), hits[1]);

        far.Children.Remove(near);
        Assert.Equal(far, Assert.Single(scene.Raycast(new(Vector3D<float>.Zero, new(2, 0, 0)))).Node);
    }

    [Fact]
    public void Raycast_reflectsComponentChangesAndHandlesInteriorAndZeroDirection()
    {
        var scene = new Scene { MainCamera = new Nexus.Graphics.Cameras.StaticCamera() };
        var node = new GameObject();
        scene.Children.Add(node);
        var ray = new Ray3D<float>(Vector3D<float>.Zero, new(1, 0, 0));
        Assert.Empty(scene.Raycast(ray));

        var shape = new SphereShape(Vector3D<float>.Zero, 1);
        node.AddComponent(shape);
        Assert.Equal(new RaycastHit(node, 0), Assert.Single(scene.Raycast(ray)));
        Assert.Empty(scene.Raycast(new(Vector3D<float>.Zero, Vector3D<float>.Zero)));

        node.RemoveComponent(shape);
        Assert.Empty(scene.Raycast(ray));
    }
}
