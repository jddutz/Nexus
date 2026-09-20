using VkBuffer = Silk.NET.Vulkan.Buffer;

namespace Nexus.Graphics.Vulkan;

/// <summary>
/// Describes a single Vulkan draw command - what to draw and how.
/// Contains all information needed for batching, state management, and rendering.
/// </summary>
public class RenderItem : IRenderItem
{
    private readonly Dictionary<ComponentId, byte[]> _componentInstanceData = [];
    private readonly List<ComponentId> _componentOrder = [];
    private byte[] _instanceData = [];
    private int _instanceStride;
    private int _instanceCount;

    /// <summary>
    /// Gets the resource identifier for this render item.
    /// </summary>
    public ResourceId Id { get; init; }

    // REQUIRED
    /// <summary>
    /// Gets the render pass mask in which this item is drawn.
    /// </summary>
    public required uint RenderMask { get; init; }

    /// <summary>
    /// Gets the graphics pipeline used to draw this item.
    /// </summary>
    public required Pipeline Pipeline { get; init; }

    /// <summary>
    /// Gets the layout associated with <see cref="Pipeline"/>.
    /// </summary>
    public required PipelineLayout Layout { get; init; }

    /// <summary>
    /// Gets the vertex buffer containing this item's geometry.
    /// </summary>
    public required VkBuffer VertexBuffer { get; init; }

    /// <summary>
    /// Gets the number of vertices to draw per instance.
    /// </summary>
    public required uint VertexCount { get; init; }

    // OPTIONAL with sensible defaults

    /// <summary>
    /// Gets the optional index buffer for this item's geometry.
    /// </summary>
    public VkBuffer IndexBuffer { get; init; }

    /// <summary>
    /// Gets the descriptor set bound while drawing this item.
    /// </summary>
    public DescriptorSet DescriptorSet { get; init; }

    /// <summary>
    /// Gets the number of descriptor sets defined by <see cref="Pipeline"/>'s descriptor schema,
    /// as reported by <see cref="Pipelines.IPipelineRegistry.GetDescriptorSetLayoutCount"/> at
    /// creation time. Authoritative for whether the camera (set 0) or material (set 1) descriptor
    /// set should be bound - a resource existing (e.g. an active camera) does not imply the
    /// pipeline's layout expects it.
    /// </summary>
    public required int DescriptorSetCount { get; init; }

    /// <summary>
    /// Gets the native index buffer handle.
    /// </summary>
    public ulong IndexBufferId => IndexBuffer.Handle;

    /// <summary>
    /// Gets the native descriptor set handle.
    /// </summary>
    public ulong DescriptorSetId => DescriptorSet.Handle;

    /// <summary>
    /// Gets the first vertex to draw from <see cref="VertexBuffer"/>.
    /// </summary>
    public uint FirstVertex { get; init; }

    /// <summary>
    /// Gets the number of active instance records.
    /// </summary>
    public uint InstanceCount => (uint)_instanceCount;

    /// <summary>
    /// Gets the byte size of a single instance record, or zero until the first record is added.
    /// </summary>
    public int InstanceStride => _instanceStride;

    /// <summary>
    /// Gets the contiguous data for all active instance records.
    /// </summary>
    public ReadOnlySpan<byte> InstanceData => _instanceData;

    /// <summary>
    /// Adds all instance records produced by the specified component.
    /// </summary>
    /// <param name="componentId">The owning component identifier.</param>
    /// <param name="component">The renderable that writes its packed instance record.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="component"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when the component already has a record or writes an invalid record size.</exception>
    public void AddInstances(ComponentId componentId, IRenderable component)
    {
        ArgumentNullException.ThrowIfNull(component);

        if (_componentInstanceData.ContainsKey(componentId))
            throw new ArgumentException(
                "Instance records already exist for this component.",
                nameof(componentId)
            );

        var packedData = PackInstances(component);
        _componentInstanceData.Add(componentId, packedData);
        _componentOrder.Add(componentId);
        RebuildFlattenedData();
    }

    /// <summary>
    /// Adds all instance records produced by a renderable component using its component identity.
    /// </summary>
    /// <param name="component">The component that writes its packed instance records.</param>
    /// <exception cref="ArgumentException">Thrown when the renderable does not also implement <see cref="IComponent"/>.</exception>
    public void AddInstances(IRenderable component)
    {
        ArgumentNullException.ThrowIfNull(component);

        if (component is not Nexus.Core.IComponent owner)
            throw new ArgumentException(
                "The renderable must implement IComponent to provide an instance owner.",
                nameof(component)
            );

        AddInstances(owner.Id, component);
    }

    /// <summary>
    /// Adds all instance records produced by the specified component.
    /// </summary>
    /// <remarks>
    /// Retained for source compatibility; despite the singular name, component ownership now
    /// applies to the component's complete instance contribution.
    /// </remarks>
    public void AddInstance(ComponentId componentId, IRenderable component) =>
        AddInstances(componentId, component);

    /// <summary>
    /// Replaces all existing instance records owned by the specified component.
    /// </summary>
    /// <param name="componentId">The owning component identifier.</param>
    /// <param name="component">The renderable that writes its replacement instance record.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="component"/> is <see langword="null"/>.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when the component has no record.</exception>
    /// <exception cref="ArgumentException">Thrown when the component writes a record whose size differs from <see cref="InstanceStride"/>.</exception>
    public void UpdateInstances(ComponentId componentId, IRenderable component)
    {
        ArgumentNullException.ThrowIfNull(component);

        if (!_componentInstanceData.ContainsKey(componentId))
            throw new KeyNotFoundException("The component has no instance records.");

        _componentInstanceData[componentId] = PackInstances(component);
        RebuildFlattenedData();
    }

    /// <summary>
    /// Replaces all instance records produced by a renderable component using its component identity.
    /// </summary>
    /// <param name="component">The component that writes its replacement instance records.</param>
    /// <exception cref="ArgumentException">Thrown when the renderable does not also implement <see cref="IComponent"/>.</exception>
    public void UpdateInstances(IRenderable component)
    {
        ArgumentNullException.ThrowIfNull(component);

        if (component is not Nexus.Core.IComponent owner)
            throw new ArgumentException(
                "The renderable must implement IComponent to provide an instance owner.",
                nameof(component)
            );

        UpdateInstances(owner.Id, component);
    }

    /// <summary>
    /// Replaces all instance records owned by the specified component.
    /// </summary>
    /// <remarks>
    /// Retained for source compatibility; despite the singular name, the complete contribution
    /// is regenerated and may change size.
    /// </remarks>
    public void UpdateInstance(ComponentId componentId, IRenderable component) =>
        UpdateInstances(componentId, component);

    /// <summary>
    /// Removes every instance record owned by the specified component.
    /// </summary>
    /// <param name="componentId">The identifier of the component that owns the record.</param>
    public void RemoveInstance(ComponentId componentId)
    {
        if (!_componentInstanceData.Remove(componentId))
            return;

        _componentOrder.Remove(componentId);
        RebuildFlattenedData();
    }

    /// <summary>
    /// Packs every instance currently produced by a component into one contribution.
    /// </summary>
    private byte[] PackInstances(IRenderable component)
    {
        var count = component.InstanceCount;
        if (count <= 0)
            throw new ArgumentException(
                "A renderable component must contribute at least one instance.",
                nameof(component)
            );

        for (var index = 0; index < count; index++)
            InitializeOrValidateStride(component.GetInstanceData(index, Span<byte>.Empty));

        var packedData = new byte[checked(count * _instanceStride)];
        for (var index = 0; index < count; index++)
        {
            var destination = packedData.AsSpan(index * _instanceStride, _instanceStride);
            ValidateStride(component.GetInstanceData(index, destination));
        }

        return packedData;
    }

    /// <summary>
    /// Rebuilds the GPU-facing sequence while retaining component contribution order.
    /// </summary>
    private void RebuildFlattenedData()
    {
        var totalLength = _componentOrder.Sum(id => _componentInstanceData[id].Length);
        var flattenedData = new byte[totalLength];
        var offset = 0;

        foreach (var componentId in _componentOrder)
        {
            var contribution = _componentInstanceData[componentId];
            contribution.CopyTo(flattenedData, offset);
            offset += contribution.Length;
        }

        _instanceData = flattenedData;
        _instanceCount = _instanceStride == 0 ? 0 : totalLength / _instanceStride;
    }

    /// <summary>
    /// Initializes the instance stride from the first record or validates a subsequent record.
    /// </summary>
    /// <param name="stride">The byte size of the packed instance record.</param>
    /// <exception cref="ArgumentException">Thrown when the record is empty or has an unexpected size.</exception>
    private void InitializeOrValidateStride(int stride)
    {
        if (_instanceStride == 0)
        {
            if (stride <= 0)
                throw new ArgumentException("An instance record cannot be empty.", nameof(stride));

            _instanceStride = stride;
            return;
        }

        ValidateStride(stride);
    }

    /// <summary>
    /// Validates that a record has the configured instance stride.
    /// </summary>
    /// <param name="stride">The byte size written by a component.</param>
    /// <exception cref="ArgumentException">Thrown when the record has an unexpected size.</exception>
    private void ValidateStride(int stride)
    {
        if (stride != _instanceStride)
            throw new ArgumentException(
                $"Instance data must be exactly {_instanceStride} bytes.",
                nameof(stride)
            );
    }

    // PUSH CONSTANTS
    /// <summary>
    /// Push constant data to send to shaders before drawing.
    /// Push constants are small amounts of data (typically up to 128 bytes) that can be
    /// updated very efficiently between draw calls.
    /// </summary>
    public object? PushConstants { get; init; }

    /// <summary>
    /// Gets the shader stages that receive <see cref="PushConstants"/>.
    /// </summary>
    public ShaderStageFlags ShaderStageFlags { get; init; } = ShaderStageFlags.All;

    // RENDER ORDERING
    /// <summary>
    /// Priority for render ordering within a RenderPass.
    /// Lower values render first. Use this for layering (e.g., background=0, scene=100, UI=1000).
    /// </summary>
    public int RenderPriority { get; init; }

    /// <summary>
    /// Distance from camera for depth sorting (typically for transparency).
    /// Higher values render first (back-to-front for correct alpha blending).
    /// Only used when batch strategy performs depth sorting.
    /// Components calculate this using RenderContext.Camera.Position in GetDrawCommands().
    /// Example: DepthSortKey = Vector3D.DistanceSquared(myPosition, context.Camera.Position)
    /// </summary>
    public float DepthSortKey { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="RenderItem"/> class.
    /// </summary>
    public RenderItem()
    {
        RenderPriority = 0;
        DepthSortKey = 0f;
    }
}
