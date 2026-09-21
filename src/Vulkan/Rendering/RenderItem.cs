namespace Nexus.Graphics.Vulkan.Rendering;

/// <summary>
/// Contains the resolved Vulkan state required to render one item across its supported passes.
/// Resource arrays are indexed by render-pass index.
/// </summary>
public class RenderItem : IRenderItem
{
    private readonly Dictionary<RenderableId, byte[]> _instanceDataByRenderable = [];
    private readonly List<RenderableId> _renderOrder = [];
    private byte[] _instanceData = [];
    private int _instanceStride;
    private int _instanceCount;

    /// <summary>
    /// Gets the resource identifier for this render item.
    /// </summary>
    public RenderableId Id { get; init; }

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
    /// Adds all instance records produced by the specified renderable.
    /// </summary>
    /// <param name="graphicsId">The graphics identifier.</param>
    /// <param name="renderable">The renderable that writes its packed instance record.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="renderable"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when the renderable already has a record or writes an invalid record size.</exception>
    public void AddInstances(IRenderable renderable)
    {
        ArgumentNullException.ThrowIfNull(renderable);
        AddInstances(renderable.Id, renderable);
    }

    /// <summary>
    /// Adds all instance records produced by the specified renderable under its identity.
    /// </summary>
    /// <param name="graphicsId">The graphics identifier.</param>
    /// <param name="renderable">The renderable that writes its packed instance record.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="renderable"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when the renderable already has a record or writes an invalid record size.</exception>
    private void AddInstances(RenderableId graphicsId, IRenderable renderable)
    {
        ArgumentNullException.ThrowIfNull(renderable);

        if (_instanceDataByRenderable.ContainsKey(graphicsId))
            throw new ArgumentException(
                "Instance records already exist for this renderable.",
                nameof(graphicsId)
            );

        var packedData = PackInstances(renderable);
        _instanceDataByRenderable.Add(graphicsId, packedData);
        _renderOrder.Add(graphicsId);
        RebuildFlattenedData();
    }

    /// <summary>
    /// Replaces all existing instance records owned by the specified renderable.
    /// </summary>
    /// <param name="graphicsId">The graphics identifier.</param>
    /// <param name="renderable">The renderable that writes its replacement instance record.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="renderable"/> is <see langword="null"/>.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when the renderable has no record.</exception>
    /// <exception cref="ArgumentException">Thrown when the renderable writes a record whose size differs from <see cref="InstanceStride"/>.</exception>
    public void UpdateInstances(IRenderable renderable)
    {
        ArgumentNullException.ThrowIfNull(renderable);
        UpdateInstances(renderable.Id, renderable);
    }

    /// <summary>
    /// Replaces all existing instance records owned by the specified renderable.
    /// </summary>
    /// <param name="graphicsId">The graphics identifier.</param>
    /// <param name="renderable">The renderable that writes its replacement instance record.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="renderable"/> is <see langword="null"/>.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when the renderable has no record.</exception>
    /// <exception cref="ArgumentException">Thrown when the renderable writes a record whose size differs from <see cref="InstanceStride"/>.</exception>
    private void UpdateInstances(RenderableId graphicsId, IRenderable renderable)
    {
        ArgumentNullException.ThrowIfNull(renderable);

        if (!_instanceDataByRenderable.ContainsKey(graphicsId))
            throw new KeyNotFoundException("The renderable has no instance records.");

        _instanceDataByRenderable[graphicsId] = PackInstances(renderable);
        RebuildFlattenedData();
    }

    /// <summary>
    /// Removes every instance record owned by the specified renderable.
    /// </summary>
    /// <param name="graphicsId">The graphics identifier whose records are removed.</param>
    public void RemoveInstances(RenderableId graphicsId)
    {
        if (!_instanceDataByRenderable.Remove(graphicsId))
            return;

        _renderOrder.Remove(graphicsId);
        RebuildFlattenedData();
    }

    /// <summary>
    /// Packs every instance currently produced by a renderable into one contribution.
    /// </summary>
    private byte[] PackInstances(IRenderable renderable)
    {
        var source = renderable.Instances;
        var count = checked((int)source.Count);
        if (count <= 0)
            return [];

        if (renderable.VertexShader is not { } vertexShader)
            return [];

        var packedData = source.GetInstanceData(vertexShader.InstanceLayout).ToArray();
        if (packedData.Length == 0 || packedData.Length % count != 0)
            throw new ArgumentException(
                "Instance data must contain a complete record for every instance.",
                nameof(renderable)
            );

        InitializeOrValidateStride(packedData.Length / count);

        return packedData;
    }

    /// <summary>
    /// Rebuilds the GPU-facing sequence while retaining renderable contribution order.
    /// </summary>
    private void RebuildFlattenedData()
    {
        var totalLength = _renderOrder.Sum(id => _instanceDataByRenderable[id].Length);
        var flattenedData = new byte[totalLength];
        var offset = 0;

        foreach (var graphicsId in _renderOrder)
        {
            var contribution = _instanceDataByRenderable[graphicsId];
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
    /// <param name="stride">The byte size written by a renderable.</param>
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
    /// Renderables calculate this using RenderContext.Camera.Position in GetDrawCommands().
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
