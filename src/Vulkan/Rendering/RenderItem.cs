namespace Nexus.Graphics.Vulkan.Rendering;

/// <summary>
/// Contains the resolved Vulkan state required to render one item across its supported passes.
/// Resource arrays are indexed by render-pass index.
/// </summary>
public class RenderItem : IRenderItem
{
    private readonly Dictionary<RenderableId, byte[]> _componentInstanceData = [];
    private readonly List<RenderableId> _renderOrder = [];
    private byte[] _instanceData = [];
    private int _instanceStride;
    private int _instanceCount;

    /// <summary>
    /// Gets the resource identifier for this render item.
    /// </summary>
    public ResourceId Id { get; init; }

    /// <summary>
    /// Gets the render passes in which this item participates.
    /// </summary>
    public required uint RenderPassMask { get; init; }

    /// <summary>Gets the graphics pipeline used for each render pass.</summary>
    public required Pipeline[] Pipelines { get; init; }

    /// <summary>Gets the pipeline layout used for each render pass.</summary>
    public required PipelineLayout[] Layouts { get; init; }

    /// <summary>Gets the vertex buffer used for each render pass.</summary>
    public required VkBuffer[] VertexBuffers { get; init; }

    /// <summary>Gets the descriptor sets used for each pass, ordered by Vulkan set number.</summary>
    public required DescriptorSet[][] DescriptorSets { get; init; }

    /// <summary>
    /// Gets the number of vertices to draw per instance.
    /// </summary>
    public required uint VertexCount { get; init; }

    /// <summary>Gets the first vertex to draw.</summary>
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
    /// <param name="renderableId">The renderable identifier.</param>
    /// <param name="component">The renderable that writes its packed instance record.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="component"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when the component already has a record or writes an invalid record size.</exception>
    public void AddInstances(RenderableId renderableId, IRenderable component)
    {
        ArgumentNullException.ThrowIfNull(component);

        if (_componentInstanceData.ContainsKey(renderableId))
            throw new ArgumentException(
                "Instance records already exist for this renderable.",
                nameof(renderableId)
            );

        var packedData = PackInstances(component);
        _componentInstanceData.Add(renderableId, packedData);
        _renderOrder.Add(renderableId);
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

        AddInstances(component.Id, component);
    }

    /// <summary>
    /// Adds all instance records produced by the specified component.
    /// </summary>
    /// <remarks>
    /// Retained for source compatibility; despite the singular name, component ownership now
    /// applies to the component's complete instance contribution.
    /// </remarks>
    public void AddInstance(RenderableId renderableId, IRenderable component) =>
        AddInstances(renderableId, component);

    /// <summary>
    /// Replaces all existing instance records owned by the specified component.
    /// </summary>
    /// <param name="renderableId">The renderable identifier.</param>
    /// <param name="component">The renderable that writes its replacement instance record.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="component"/> is <see langword="null"/>.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when the component has no record.</exception>
    /// <exception cref="ArgumentException">Thrown when the component writes a record whose size differs from <see cref="InstanceStride"/>.</exception>
    public void UpdateInstances(RenderableId renderableId, IRenderable component)
    {
        ArgumentNullException.ThrowIfNull(component);

        if (!_componentInstanceData.ContainsKey(renderableId))
            throw new KeyNotFoundException("The renderable has no instance records.");

        _componentInstanceData[renderableId] = PackInstances(component);
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

        UpdateInstances(component.Id, component);
    }

    /// <summary>
    /// Replaces all instance records owned by the specified component.
    /// </summary>
    /// <remarks>
    /// Retained for source compatibility; despite the singular name, the complete contribution
    /// is regenerated and may change size.
    /// </remarks>
    public void UpdateInstance(RenderableId renderableId, IRenderable component) =>
        UpdateInstances(renderableId, component);

    /// <summary>
    /// Removes every instance record owned by the specified component.
    /// </summary>
    /// <param name="renderableId">The identifier of the renderable whose records are removed.</param>
    public void RemoveInstance(RenderableId renderableId)
    {
        if (!_componentInstanceData.Remove(renderableId))
            return;

        _renderOrder.Remove(renderableId);
        RebuildFlattenedData();
    }

    /// <summary>
    /// Packs every instance currently produced by a component into one contribution.
    /// </summary>
    private byte[] PackInstances(IRenderable component)
    {
        var source = component.Instances;
        var count = checked((int)source.Count);
        if (count <= 0)
            throw new ArgumentException(
                "A renderable component must contribute at least one instance.",
                nameof(component)
            );

        var packedData = source.GetInstanceData(component.VertexShader.InstanceLayout).ToArray();
        if (packedData.Length == 0 || packedData.Length % count != 0)
            throw new ArgumentException(
                "Instance data must contain a complete record for every instance.",
                nameof(component)
            );

        InitializeOrValidateStride(packedData.Length / count);

        return packedData;
    }

    /// <summary>
    /// Rebuilds the GPU-facing sequence while retaining component contribution order.
    /// </summary>
    private void RebuildFlattenedData()
    {
        var totalLength = _renderOrder.Sum(id => _componentInstanceData[id].Length);
        var flattenedData = new byte[totalLength];
        var offset = 0;

        foreach (var componentId in _renderOrder)
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
