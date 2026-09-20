using System.Buffers.Binary;
using Nexus.Core;
using Nexus.Graphics;
using Nexus.Graphics.Vulkan;

namespace Tests;

/// <summary>
/// Verifies component ownership and flattening of render-item instance records.
/// </summary>
public class RenderItemInstanceTests
{
    [Fact]
    public void SingleInstanceComponentAddsOneInstance()
    {
        var item = CreateRenderItem();

        item.AddInstances(new TestRenderable(10));

        Assert.Equal(1u, item.InstanceCount);
        Assert.Equal([10], ReadValues(item));
    }

    [Fact]
    public void CompatibleSingleInstanceComponentsShareItem()
    {
        var item = CreateRenderItem();

        item.AddInstances(new TestRenderable(10));
        item.AddInstances(new TestRenderable(20));

        Assert.Equal(2u, item.InstanceCount);
        Assert.Equal([10, 20], ReadValues(item));
    }

    [Fact]
    public void MultiInstanceComponentAddsAllInstancesToOneItem()
    {
        var item = CreateRenderItem();

        item.AddInstances(new TestRenderable(1, 2, 3, 4));

        Assert.Equal(4u, item.InstanceCount);
        Assert.Equal([1, 2, 3, 4], ReadValues(item));
    }

    [Fact]
    public void UpdateReplacesSameNumberOfInstances()
    {
        var item = CreateRenderItem();
        var component = new TestRenderable(1, 2, 3);
        item.AddInstances(component);

        component.Values = [4, 5, 6];
        item.UpdateInstances(component);

        Assert.Equal(3u, item.InstanceCount);
        Assert.Equal([4, 5, 6], ReadValues(item));
    }

    [Theory]
    [InlineData(new[] { 1, 2 }, new[] { 3, 4, 5, 6 })]
    [InlineData(new[] { 1, 2, 3, 4 }, new[] { 5, 6 })]
    public void UpdateChangesContributionSize(int[] original, int[] replacement)
    {
        var item = CreateRenderItem();
        var component = new TestRenderable(original);
        var other = new TestRenderable(99);
        item.AddInstances(component);
        item.AddInstances(other);

        component.Values = replacement;
        item.UpdateInstances(component);

        Assert.Equal((uint)(replacement.Length + 1), item.InstanceCount);
        Assert.Equal([.. replacement, 99], ReadValues(item));
    }

    [Fact]
    public void RemoveDeletesAllOwnedInstancesWithoutAffectingOthers()
    {
        var item = CreateRenderItem();
        var removed = new TestRenderable(1, 2, 3);
        item.AddInstances(removed);
        item.AddInstances(new TestRenderable(10, 20));

        item.RemoveInstance(removed.Id);

        Assert.Equal(2u, item.InstanceCount);
        Assert.Equal([10, 20], ReadValues(item));
    }

    [Fact]
    public void RecreateMovesCompleteContributionBetweenItems()
    {
        var oldItem = CreateRenderItem();
        var newItem = CreateRenderItem();
        var component = new TestRenderable(1, 2, 3);
        oldItem.AddInstances(component);

        oldItem.RemoveInstance(component.Id);
        newItem.AddInstances(component);

        Assert.Equal(0u, oldItem.InstanceCount);
        Assert.Empty(oldItem.InstanceData.ToArray());
        Assert.Equal(3u, newItem.InstanceCount);
        Assert.Equal([1, 2, 3], ReadValues(newItem));
    }

    [Fact]
    public void DrawInstanceCountUsesFlattenedRecordCount()
    {
        var item = CreateRenderItem();
        item.AddInstances(new TestRenderable(1, 2, 3));
        item.AddInstances(new TestRenderable(4, 5));

        Assert.Equal(5u, item.InstanceCount);
    }

    private static int[] ReadValues(RenderItem item)
    {
        var data = item.InstanceData;
        var values = new int[checked((int)item.InstanceCount)];
        for (var index = 0; index < values.Length; index++)
            values[index] = BinaryPrimitives.ReadInt32LittleEndian(data[(index * sizeof(int))..]);

        return values;
    }

    private static RenderItem CreateRenderItem() =>
        new()
        {
            RenderMask = 1,
            Pipeline = default,
            Layout = default,
            VertexBuffer = default,
            VertexCount = 4,
            DescriptorSetCount = 0,
        };

    private sealed class TestRenderable(params int[] values) : Component, IRenderable
    {
        public int[] Values { get; set; } = values;

        public IEnumerable<RenderLayer> RenderLayers => [];

        public int InstanceCount => Values.Length;

        public int GetInstanceData(Span<byte> destination) => GetInstanceData(0, destination);

        public int GetInstanceData(int instanceIndex, Span<byte> destination)
        {
            if ((uint)instanceIndex >= (uint)Values.Length)
                throw new ArgumentOutOfRangeException(nameof(instanceIndex));

            if (destination.Length >= sizeof(int))
                BinaryPrimitives.WriteInt32LittleEndian(destination, Values[instanceIndex]);

            return sizeof(int);
        }
    }
}
