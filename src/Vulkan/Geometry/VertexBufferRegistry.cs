namespace Nexus.Graphics.Vulkan.Geometry;

/// <summary>
/// Manages Vulkan vertex buffers keyed by geometry and vertex-format identities.
/// </summary>
public unsafe class VertexBufferRegistry : IVertexBufferRegistry
{
    private readonly Context _context;
    private readonly ILogger<VertexBufferRegistry> _logger;
    private readonly ISyncManager _syncManager;

    private readonly Dictionary<ulong, VkBuffer> _buffers = [];
    private readonly Dictionary<VkBuffer, DeviceMemory> _memory = [];
    private readonly Dictionary<VkBuffer, int> _refs = [];
    private readonly Queue<VkBuffer>[] _released;

    /// <summary>
    /// Creates a geometry registry with frame-slot deferred-release queues.
    /// </summary>
    /// <param name="context">The Vulkan context that owns the buffers.</param>
    /// <param name="logger">The logger used to record buffer lifecycle events.</param>
    /// <param name="syncManager">The synchronization manager used to defer buffer destruction.</param>
    public VertexBufferRegistry(
        Context context,
        ILogger<VertexBufferRegistry> logger,
        ISyncManager syncManager
    )
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _syncManager = syncManager ?? throw new ArgumentNullException(nameof(syncManager));
        _released = new Queue<VkBuffer>[checked((int)syncManager.MaxFramesInFlight)];

        for (var index = 0; index < _released.Length; index++)
            _released[index] = new Queue<VkBuffer>();

        _syncManager.FrameCompleted += OnFrameCompleted;
    }

    /// <inheritdoc/>
    public IEnumerable<IVulkanCommand> Create(IGeometry geometry, VertexFormat format)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(format);

        var key = ComputeVertexBufferId(geometry.Id, format.Id);

        if (_buffers.TryGetValue(key, out var buffer))
        {
            var referenceCount = ++_refs[buffer];

            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug(
                    "Reusing vertex buffer. MeshId={MeshId}, VertexFormatId={VertexFormatId}, BufferHandle={BufferHandle}, ReferenceCount={ReferenceCount}",
                    geometry.Id,
                    format.Id,
                    buffer.Handle,
                    referenceCount
                );
        }
        else
        {
            var data = new byte[checked((int)geometry.Count * (int)format.Stride)];
            geometry.WriteTo(0, checked((int)geometry.Count), format, data);

            buffer = CreateBuffer(data);

            _buffers.Add(key, buffer);
            _refs.Add(buffer, 1);

            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug(
                    "Created vertex buffer. MeshId={MeshId}, VertexFormatId={VertexFormatId}, BufferHandle={BufferHandle}, Size={Size}",
                    geometry.Id,
                    format.Id,
                    buffer.Handle,
                    data.Length
                );
        }

        return [];
    }

    /// <inheritdoc/>
    public IEnumerable<IVulkanCommand> Update(IGeometry geometry, VertexFormat format)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(format);

        var key = ComputeVertexBufferId(geometry.Id, format.Id);

        if (!_buffers.TryGetValue(key, out var oldBuffer))
            return Create(geometry, format);

        var data = new byte[checked((int)geometry.Count * (int)format.Stride)];
        geometry.WriteTo(0, checked((int)geometry.Count), format, data);

        var newBuffer = CreateBuffer(data);
        var referenceCount = _refs[oldBuffer];

        _buffers[key] = newBuffer;
        _refs.Remove(oldBuffer);
        _refs.Add(newBuffer, referenceCount);
        QueueRelease(oldBuffer);

        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug(
                "Updated vertex buffer. MeshId={MeshId}, VertexFormatId={VertexFormatId}, OldBufferHandle={OldBufferHandle}, NewBufferHandle={NewBufferHandle}, Size={Size}, ReferenceCount={ReferenceCount}",
                geometry.Id,
                format.Id,
                oldBuffer.Handle,
                newBuffer.Handle,
                data.Length,
                referenceCount
            );

        return [];
    }

    /// <inheritdoc/>
    public IEnumerable<IVulkanCommand> Release(IGeometry geometry, VertexFormat format)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(format);

        var key = ComputeVertexBufferId(geometry.Id, format.Id);

        if (!_buffers.TryGetValue(key, out var buffer))
            return [];

        var referenceCount = --_refs[buffer];

        if (referenceCount > 0)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug(
                    "Released vertex buffer reference. MeshId={MeshId}, VertexFormatId={VertexFormatId}, BufferHandle={BufferHandle}, ReferenceCount={ReferenceCount}",
                    geometry.Id,
                    format.Id,
                    buffer.Handle,
                    referenceCount
                );

            return [];
        }

        _refs.Remove(buffer);
        _buffers.Remove(key);
        QueueRelease(buffer);

        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug(
                "Queued vertex buffer release. MeshId={MeshId}, VertexFormatId={VertexFormatId}, BufferHandle={BufferHandle}",
                geometry.Id,
                format.Id,
                buffer.Handle
            );

        return [];
    }

    /// <summary>
    /// Gets the Vulkan vertex buffer for a geometry and vertex-format identity.
    /// </summary>
    /// <param name="meshId">The geometry identifier.</param>
    /// <param name="formatId">The vertex-format identifier.</param>
    /// <returns>The registered Vulkan vertex buffer.</returns>
    public VkBuffer Get(MeshId meshId, VertexFormatId formatId)
    {
        var key = ComputeVertexBufferId(meshId, formatId);

        if (!_buffers.TryGetValue(key, out var buffer))
            throw new KeyNotFoundException(
                $"Vertex buffer for mesh '{meshId}' and vertex format '{formatId}' is not registered."
            );

        return buffer;
    }

    /// <summary>
    /// Computes the identity of a vertex buffer from its mesh and vertex format.
    /// </summary>
    /// <param name="meshId">The mesh identity.</param>
    /// <param name="formatId">The vertex format identity.</param>
    /// <returns>The computed vertex-buffer identity.</returns>
    private static ulong ComputeVertexBufferId(MeshId meshId, VertexFormatId formatId) =>
        new IdentityHashBuilder("VertexBufferId").Add(meshId).Add(formatId).Compute();

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
            {
                _context.VulkanApi.FreeMemory(_context.Device, memory, null);
            }
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
            {
                return index;
            }
        }

        throw new InvalidOperationException(
            $"Unable to find Vulkan memory type with properties '{properties}'."
        );
    }

    /// <summary>
    /// Creates a host-visible Vulkan vertex buffer and uploads serialized geometry data.
    /// </summary>
    /// <param name="data">The serialized vertex data.</param>
    /// <returns>The created Vulkan buffer.</returns>
    private VkBuffer CreateBuffer(ReadOnlyMemory<byte> data)
    {
        if (data.IsEmpty)
            throw new InvalidOperationException("Vertex data cannot be empty.");

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
            throw new InvalidOperationException($"Unable to create vertex buffer: {result}");

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
                throw new InvalidOperationException($"Unable to allocate vertex memory: {result}");

            result = _context.VulkanApi.BindBufferMemory(_context.Device, buffer, memory, 0);

            if (result != Result.Success)
                throw new InvalidOperationException($"Unable to bind vertex memory: {result}");

            void* mapped = null;

            result = _context.VulkanApi.MapMemory(_context.Device, memory, 0, size, 0, &mapped);

            if (result != Result.Success)
                throw new InvalidOperationException($"Unable to map vertex memory: {result}");

            try
            {
                fixed (byte* source = data.Span)
                {
                    System.Buffer.MemoryCopy(
                        source,
                        mapped,
                        checked((long)size),
                        checked((long)size)
                    );
                }
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

    /// <summary>
    /// Destroys every managed Vulkan vertex buffer and its backing memory.
    /// </summary>
    public void Reset()
    {
        foreach (var memoryEntry in _memory)
        {
            _context.VulkanApi.DestroyBuffer(_context.Device, memoryEntry.Key, null);
            _context.VulkanApi.FreeMemory(_context.Device, memoryEntry.Value, null);
        }

        _buffers.Clear();
        _memory.Clear();
        _refs.Clear();

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
