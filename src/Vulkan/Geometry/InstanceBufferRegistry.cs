namespace Nexus.Graphics.Vulkan.Geometry;

/// <summary>
/// Manages Vulkan vertex buffers containing drawable instance data.
/// </summary>
public unsafe class InstanceBufferRegistry : IInstanceBufferRegistry
{
    private readonly Context _context;
    private readonly ISyncManager _syncManager;
    private readonly Dictionary<DrawableId, VkBuffer> _buffers = [];
    private readonly Dictionary<VkBuffer, DeviceMemory> _memory = [];
    private readonly Queue<VkBuffer>[] _released;

    /// <summary>
    /// Creates an instance-buffer registry with frame-slot deferred-release queues.
    /// </summary>
    /// <param name="context">The Vulkan context that owns the buffers.</param>
    /// <param name="syncManager">The synchronization manager used to defer buffer destruction.</param>
    public InstanceBufferRegistry(Context context, ISyncManager syncManager)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _syncManager = syncManager ?? throw new ArgumentNullException(nameof(syncManager));
        _released = new Queue<VkBuffer>[checked((int)syncManager.MaxFramesInFlight)];

        for (var index = 0; index < _released.Length; index++)
            _released[index] = new Queue<VkBuffer>();

        _syncManager.FrameCompleted += OnFrameCompleted;
    }

    /// <inheritdoc/>
    public IEnumerable<IVulkanCommand> Create(IDrawable drawable, ShaderInput[] layout)
    {
        ArgumentNullException.ThrowIfNull(drawable);
        ArgumentNullException.ThrowIfNull(layout);

        var data = drawable.Instances.GetInstanceData(layout);
        if (data.IsEmpty)
            throw new InvalidOperationException("Instance data cannot be empty.");

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

    /// <summary>
    /// Queues a buffer for destruction when the selected frame slot completes.
    /// </summary>
    /// <param name="buffer">The buffer to release.</param>
    private void QueueRelease(VkBuffer buffer)
    {
        var releaseFrameIndex = checked(
            (int)(
                (_syncManager.CurrentFrameIndex + _syncManager.MaxFramesInFlight - 1)
                % _syncManager.MaxFramesInFlight
            )
        );

        _released[releaseFrameIndex].Enqueue(buffer);
    }

    /// <summary>
    /// Destroys buffers queued for the completed frame slot.
    /// </summary>
    /// <param name="sender">The synchronization manager.</param>
    /// <param name="e">The completed frame event data.</param>
    private void OnFrameCompleted(object? sender, FrameCompletedEventArgs e)
    {
        var releaseFrameIndex = checked((int)e.FrameIndex);
        if (releaseFrameIndex >= _released.Length)
            throw new ArgumentOutOfRangeException(nameof(e), e.FrameIndex, "Invalid frame index.");

        var queue = _released[releaseFrameIndex];
        while (queue.TryDequeue(out var buffer))
        {
            _context.VulkanApi.DestroyBuffer(_context.Device, buffer, null);

            if (_memory.Remove(buffer, out var memory))
                _context.VulkanApi.FreeMemory(_context.Device, memory, null);
        }
    }

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
        }

        _buffers.Clear();
        _memory.Clear();
        foreach (var queue in _released)
            queue.Clear();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _syncManager.FrameCompleted -= OnFrameCompleted;
        Reset();
        GC.SuppressFinalize(this);
    }
}
