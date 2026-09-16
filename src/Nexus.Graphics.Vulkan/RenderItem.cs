using VkBuffer = Silk.NET.Vulkan.Buffer;

namespace Nexus.Graphics.Vulkan;

/// <summary>
/// Describes a single Vulkan draw command - what to draw and how.
/// Contains all information needed for batching, state management, and rendering.
/// </summary>
public class RenderItem : IRenderItem
{
    private readonly Dictionary<ComponentId, int> _instanceSlots = [];
    private readonly List<ComponentId> _slotComponents = [];
    private byte[] _instanceData = [];
    private int _instanceStride;

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
    public uint InstanceCount => (uint)_instanceSlots.Count;

    /// <summary>
    /// Gets the byte size of a single instance record, or zero until the first record is added.
    /// </summary>
    public int InstanceStride => _instanceStride;

    /// <summary>
    /// Gets the contiguous data for all active instance records.
    /// </summary>
    public ReadOnlySpan<byte> InstanceData =>
        _instanceData.AsSpan(0, checked(_instanceSlots.Count * _instanceStride));

    /// <summary>
    /// Adds an instance record for the specified component.
    /// </summary>
    /// <param name="component">The component that writes its packed instance record.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="component"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when the component already has a record or writes an invalid record size.</exception>
    public void AddInstance(IRenderableComponent component)
    {
        ArgumentNullException.ThrowIfNull(component);

        if (_instanceSlots.ContainsKey(component.Id))
            throw new ArgumentException(
                "An instance record already exists for this component.",
                nameof(component)
            );

        InitializeOrValidateStride(component.GetInstanceData(Span<byte>.Empty));
        EnsureCapacity(_instanceSlots.Count + 1);

        var slot = _instanceSlots.Count;
        var destination = _instanceData.AsSpan(slot * _instanceStride, _instanceStride);
        ValidateStride(component.GetInstanceData(destination));

        _instanceSlots.Add(component.Id, slot);
        _slotComponents.Add(component.Id);
    }

    /// <summary>
    /// Updates the existing instance record for the specified component.
    /// </summary>
    /// <param name="component">The component that writes its replacement instance record.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="component"/> is <see langword="null"/>.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when the component has no record.</exception>
    /// <exception cref="ArgumentException">Thrown when the component writes a record whose size differs from <see cref="InstanceStride"/>.</exception>
    public void UpdateInstance(IRenderableComponent component)
    {
        ArgumentNullException.ThrowIfNull(component);

        if (!_instanceSlots.TryGetValue(component.Id, out var slot))
            throw new KeyNotFoundException("The component has no instance record.");

        var destination = _instanceData.AsSpan(slot * _instanceStride, _instanceStride);
        ValidateStride(component.GetInstanceData(destination));
    }

    /// <summary>
    /// Removes the instance record for the specified component.
    /// </summary>
    /// <param name="componentId">The identifier of the component that owns the record.</param>
    /// <returns><see langword="true"/> when a record was removed; otherwise, <see langword="false"/>.</returns>
    public void RemoveInstance(ComponentId componentId)
    {
        if (!_instanceSlots.Remove(componentId, out var removedSlot))
            return;

        var lastSlot = _slotComponents.Count - 1;
        if (removedSlot == lastSlot)
        {
            _slotComponents.RemoveAt(lastSlot);
            return;
        }

        _instanceData
            .AsSpan(lastSlot * _instanceStride, _instanceStride)
            .CopyTo(_instanceData.AsSpan(removedSlot * _instanceStride, _instanceStride));

        var movedComponentId = _slotComponents[lastSlot];
        _instanceSlots[movedComponentId] = removedSlot;
        _slotComponents[removedSlot] = movedComponentId;
        _slotComponents.RemoveAt(lastSlot);
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

    /// <summary>
    /// Ensures that the backing store can contain the requested number of records.
    /// </summary>
    /// <param name="requiredInstanceCount">The required number of records.</param>
    private void EnsureCapacity(int requiredInstanceCount)
    {
        var requiredLength = checked(requiredInstanceCount * _instanceStride);
        if (_instanceData.Length >= requiredLength)
            return;

        var newLength = Math.Max(
            requiredLength,
            Math.Max(_instanceStride, _instanceData.Length * 2)
        );
        Array.Resize(ref _instanceData, newLength);
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
