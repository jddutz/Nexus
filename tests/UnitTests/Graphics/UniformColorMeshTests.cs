namespace Tests;

using System.Runtime.InteropServices;
using Nexus.Graphics;
using Nexus.Graphics.Geometry;
using Nexus.Graphics.Shaders;
using Silk.NET.Maths;

public class UniformColorMeshTests
{
    private static UniformColorMesh Create() => new(new Mesh("test.instances",
        PrimitiveTopologyEnum.TriangleList, [new(new(0f, 0f, 0f))]));

    [Fact]
    public void Instances_pack_requested_range_and_copy_input()
    {
        var drawable = Create();
        var transform = Matrix4X4.CreateTranslation(15f, 20f, 0f);
        var color = new Color(0.7f, 0.2f, 0.1f);
        UniformColorMeshInstance[] instances =
        [new(Matrix4X4<float>.Identity, Colors.Black), new(transform, color)];
        drawable.SetInstances(instances);
        instances[1] = default;
        Assert.Equal(2UL, drawable.InstanceCount);
        var data = new byte[80];
        drawable.WriteInstanceDataTo(1, 1, BuiltInShaders.UniformColorTriangleListVertexShader.InstanceLayout, data);
        Assert.Equal(transform, MemoryMarshal.Read<Matrix4X4<float>>(data));
        Assert.Equal(color, MemoryMarshal.Read<Color>(data.AsSpan(64)));
        Assert.Throws<ArgumentOutOfRangeException>(() => drawable.WriteInstanceDataTo(2, 1, [], data));
        Assert.Throws<ArgumentException>(() => drawable.WriteInstanceDataTo(0, 2,
            BuiltInShaders.UniformColorTriangleListVertexShader.InstanceLayout, data));
    }

    [Fact]
    public void Instances_notify_only_on_change_and_restore_single_instance()
    {
        var drawable = Create();
        var notifications = 0;
        drawable.InstanceDataChanged += (_, _) => notifications++;
        drawable.SetInstances([]);
        drawable.SetInstances([]);
        Assert.Equal(0UL, drawable.InstanceCount);
        Assert.Equal(1, notifications);
        drawable.SetInstances(null);
        Assert.Equal(1UL, drawable.InstanceCount);
        Assert.Equal(2, notifications);
    }
}
