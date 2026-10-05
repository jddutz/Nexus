using Nexus.Core.Performance;
namespace Nexus.Graphics.Vulkan.Geometry;

/// <summary>
/// Manages Vulkan vertex buffers containing drawable instance data.
/// </summary>
public unsafe class InstanceBufferRegistry : IInstanceBufferRegistry
{
    private readonly IPerformanceTelemetry? _telemetry;
    private readonly Context _context;
    private readonly ISyncManager _syncManager;
    private readonly PerformanceMetrics? _performanceMetrics;
    private readonly FrameRetirementQueue<VkBuffer> _retiredBuffers;
    private readonly Dictionary<DrawableId, VkBuffer> _buffers = [];
    private readonly Dictionary<VkBuffer, DeviceMemory> _memory = [];

    /// <summary>
    /// Creates an instance-buffer registry with frame-slot deferred-release queues.
    /// </summary>
    /// <param name="context">The Vulkan context that owns the buffers.</param>
    /// <param name="syncManager">The synchronization manager used to defer buffer destruction.</param>
    /// <param name="performanceMetrics">Optional metrics tracker for live Vulkan buffers.</param>
    public InstanceBufferRegistry(
        Context context,
        ISyncManager syncManager,
        PerformanceMetrics? performanceMetrics = null,
        IPerformanceTelemetry? telemetry = null
    )
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _syncManager = syncManager ?? throw new ArgumentNullException(nameof(syncManager));
        _performanceMetrics = performanceMetrics;
        _telemetry = telemetry;
        _retiredBuffers = new FrameRetirementQueue<VkBuffer>(
            syncManager.MaxFramesInFlight,
            DestroyBuffer
        );

        _syncManager.FrameCompleted += OnFrameCompleted;
        _syncManager.FrameSubmitted += OnFrameSubmitted;
    }

    /// <inheritdoc/>
    public IEnumerable<IVulkanCommand> Create(IDrawable drawable, ShaderInput[] layout)
    {
        ArgumentNullException.ThrowIfNull(drawable);
        ArgumentNullException.ThrowIfNull(layout);

        using var timing = new LoadPerformanceScope(_telemetry, "geometry.instances.realize", units: checked((long)drawable.InstanceCount));
        var stride = checked((ulong)layout.Sum(input => input.Size));
        var dataLength = checked((int)(drawable.InstanceCount * stride));
        var data = new byte[dataLength == 0 ? checked((int)Math.Max(stride, 1UL)) : dataLength];
        using (var serialization = new LoadPerformanceScope(_telemetry, "geometry.instances.serialize", units: data.Length))
            drawable.WriteInstanceDataTo(0, drawable.InstanceCount, layout, data);

        var newBuffer = CreateBuffer(data);

        if (_buffers.Remove(drawable.Id, out var oldBuffer))
            QueueRelease(oldBuffer);

        _buffers.Add(drawable.Id, newBuffer);

        Debug.WriteLine(
            $"Created instance buffer. DrawableId={drawable.Id}, BufferHandle={newBuffer.Handle}, Size={data.Length}"
        );

        return [];
    }

    /// <inheritdoc/>
    public VkBuffer Get(DrawableId drawableId)
    {
        if (!_buffers.TryGetValue(drawableId, out var buffer))
            throw new KeyNotFoundException(
                $"Instance buffer for drawable '{drawableId}' is not registered."
            );

        return buffer;
    }

    /// <inheritdoc/>
    public IEnumerable<IVulkanCommand> Update(IDrawable drawable, ShaderInput[] layout)
    {
        ArgumentNullException.ThrowIfNull(drawable);
        ArgumentNullException.ThrowIfNull(layout);

        if (!_buffers.TryGetValue(drawable.Id, out var buffer))
            throw new KeyNotFoundException(
                $"Instance buffer for drawable '{drawable.Id}' is not registered."
            );

        var stride = checked((ulong)layout.Sum(input => input.Size));
        var data = new byte[checked((int)(drawable.InstanceCount * stride))];
        if (data.Length == 0)
            return [];

        drawable.WriteInstanceDataTo(0, drawable.InstanceCount, layout, data);
        var replacement = CreateBuffer(data);
        _buffers[drawable.Id] = replacement;
        QueueRelease(buffer);

        return [];
    }

    /// <inheritdoc/>
    public IEnumerable<IVulkanCommand> Release(DrawableId drawableId)
    {
        if (!_buffers.Remove(drawableId, out var buffer))
            return [];

        QueueRelease(buffer);

        Debug.WriteLine(
            $"Queued instance buffer release. DrawableId={drawableId}, BufferHandle={buffer.Handle}"
        );

        return [];
    }

    /// <summary>Defers destruction until every submitted frame that may use the buffer completes.</summary>
    /// <param name="buffer">The buffer to release.</param>
    private void QueueRelease(VkBuffer buffer) => _retiredBuffers.Retire(buffer);

    /// <summary>Destroys a retired buffer after all referencing frame slots complete.</summary>
    /// <param name="buffer">The buffer whose final GPU use has completed.</param>
    private void DestroyBuffer(VkBuffer buffer)
    {
        _context.VulkanApi.DestroyBuffer(_context.Device, buffer, null);
        if (_memory.Remove(buffer, out var memory))
        {
            _context.VulkanApi.FreeMemory(_context.Device, memory, null);
            _performanceMetrics?.RecordBufferDestroyed();
        }
    }

    /// <summary>Records a completed frame slot for deferred buffer retirement.</summary>
    /// <param name="sender">The synchronization manager.</param>
    /// <param name="e">The completed frame event data.</param>
    private void OnFrameCompleted(object? sender, FrameCompletedEventArgs e)
        => _retiredBuffers.OnFrameCompleted(e.FrameIndex);

    /// <summary>Records a submitted frame slot that may reference active instance buffers.</summary>
    /// <param name="sender">The synchronization manager.</param>
    /// <param name="e">The submitted frame event data.</param>
    private void OnFrameSubmitted(object? sender, FrameSubmittedEventArgs e) =>
        _retiredBuffers.OnFrameSubmitted(e.FrameIndex);

    /// <summary>
    /// Finds a physical-device memory type that satisfies the requested properties.
    /// </summary>
    /// <param name="typeFilter">The bitmask of compatible memory-type indices.</param>
    /// <param name="properties">The required memory properties.</param>
    /// <returns>The selected memory-type index.</returns>
    private uint FindMemoryType(uint typeFilter, MemoryPropertyFlags properties)
    {
        _context.VulkanApi.GetPhysicalDeviceMemoryProperties(
            _context.PhysicalDevice,
            out var memoryProperties
        );

        for (uint index = 0; index < memoryProperties.MemoryTypeCount; index++)
        {
            if (
                (typeFilter & (1u << (int)index)) != 0
                && (memoryProperties.MemoryTypes[(int)index].PropertyFlags & properties)
                    == properties
            )
                return index;
        }

        throw new InvalidOperationException(
            $"Unable to find Vulkan memory type with properties '{properties}'."
        );
    }

    /// <summary>
    /// Creates a host-visible Vulkan instance buffer and uploads serialized instance data.
    /// </summary>
    /// <param name="data">The serialized instance data.</param>
    /// <returns>The created Vulkan buffer.</returns>
    private VkBuffer CreateBuffer(ReadOnlyMemory<byte> data)
    {
        if (data.IsEmpty)
            throw new InvalidOperationException("Instance data cannot be empty.");

        var size = checked((ulong)data.Length);
        var bufferInfo = new BufferCreateInfo
        {
            SType = StructureType.BufferCreateInfo,
            Size = size,
            Usage = BufferUsageFlags.VertexBufferBit,
            SharingMode = SharingMode.Exclusive,
        };

        var result = _context.VulkanApi.CreateBuffer(
            _context.Device,
            in bufferInfo,
            null,
            out var buffer
        );
        if (result != Result.Success)
            throw new InvalidOperationException($"Unable to create instance buffer: {result}");

        DeviceMemory memory = default;
        try
        {
            _context.VulkanApi.GetBufferMemoryRequirements(
                _context.Device,
                buffer,
                out var requirements
            );
            var allocationInfo = new MemoryAllocateInfo
            {
                SType = StructureType.MemoryAllocateInfo,
                AllocationSize = requirements.Size,
                MemoryTypeIndex = FindMemoryType(
                    requirements.MemoryTypeBits,
                    MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit
                ),
            };
            result = _context.VulkanApi.AllocateMemory(
                _context.Device,
                in allocationInfo,
                null,
                out memory
            );
            if (result != Result.Success)
                throw new InvalidOperationException(
                    $"Unable to allocate instance memory: {result}"
                );

            result = _context.VulkanApi.BindBufferMemory(_context.Device, buffer, memory, 0);
            if (result != Result.Success)
                throw new InvalidOperationException($"Unable to bind instance memory: {result}");

            void* mapped = null;
            result = _context.VulkanApi.MapMemory(_context.Device, memory, 0, size, 0, &mapped);
            if (result != Result.Success)
                throw new InvalidOperationException($"Unable to map instance memory: {result}");

            try
            {
                fixed (byte* source = data.Span)
                    System.Buffer.MemoryCopy(
                        source,
                        mapped,
                        checked((long)size),
                        checked((long)size)
                    );
            }
            finally
            {
                _context.VulkanApi.UnmapMemory(_context.Device, memory);
            }

            _memory.Add(buffer, memory);
            _performanceMetrics?.RecordBufferCreated();
            return buffer;
        }
        catch
        {
            if (memory.Handle != 0)
                _context.VulkanApi.FreeMemory(_context.Device, memory, null);
            _context.VulkanApi.DestroyBuffer(_context.Device, buffer, null);
            throw;
        }
    }

    /// <inheritdoc/>
    public void Reset()
    {
        foreach (var memoryEntry in _memory)
        {
            _context.VulkanApi.DestroyBuffer(_context.Device, memoryEntry.Key, null);
            _context.VulkanApi.FreeMemory(_context.Device, memoryEntry.Value, null);
            _performanceMetrics?.RecordBufferDestroyed();
        }

        _buffers.Clear();
        _memory.Clear();
        _retiredBuffers.Reset();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _syncManager.FrameCompleted -= OnFrameCompleted;
        _syncManager.FrameSubmitted -= OnFrameSubmitted;
        Reset();
        GC.SuppressFinalize(this);
    }
}
