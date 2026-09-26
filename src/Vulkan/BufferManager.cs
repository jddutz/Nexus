namespace Nexus.Graphics.Vulkan;

/// <summary>
/// Implements <see cref="IBufferManager"/> using Vulkan buffer and device-memory resources.
/// </summary>
/// <remarks>
/// Initializes a buffer manager for the specified Vulkan context.
/// </remarks>
/// <param name="context">The Vulkan context used to create and manage buffers.</param>
public unsafe class BufferManager(Context context, PerformanceMetrics? performanceMetrics = null)
    : IBufferManager
{
    // Tracks the DeviceMemory backing each buffer, keyed by the buffer's native handle
    // (which also serves as the buffer's VkBuffer value).
    private readonly ConcurrentDictionary<ulong, (DeviceMemory Memory, ulong Size)> _buffers =
        new();
    private readonly Context _context = context;
    private readonly PerformanceMetrics? _performanceMetrics = performanceMetrics;

    /// <inheritdoc />
    public VkBuffer CreateVertexBuffer(ReadOnlySpan<byte> data)
    {
        var bufferSize = (ulong)data.Length;

        // Create buffer
        var bufferInfo = new BufferCreateInfo
        {
            SType = StructureType.BufferCreateInfo,
            Size = bufferSize,
            Usage = BufferUsageFlags.VertexBufferBit,
            SharingMode = SharingMode.Exclusive,
        };

        VkBuffer buffer;
        if (
            _context.VulkanApi.CreateBuffer(_context.Device, &bufferInfo, null, &buffer)
            != Result.Success
        )
        {
            throw new Exception("Failed to create vertex buffer");
        }

        // Allocate memory
        _context.VulkanApi.GetBufferMemoryRequirements(
            _context.Device,
            buffer,
            out var memRequirements
        );

        var allocInfo = new MemoryAllocateInfo
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = memRequirements.Size,
            MemoryTypeIndex = FindMemoryType(
                memRequirements.MemoryTypeBits,
                MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit
            ),
        };

        DeviceMemory memory;
        if (
            _context.VulkanApi.AllocateMemory(_context.Device, &allocInfo, null, &memory)
            != Result.Success
        )
        {
            _context.VulkanApi.DestroyBuffer(_context.Device, buffer, null);
            throw new Exception("Failed to allocate vertex buffer memory");
        }

        // Bind and upload
        _context.VulkanApi.BindBufferMemory(_context.Device, buffer, memory, 0);

        void* mappedData;
        _context.VulkanApi.MapMemory(_context.Device, memory, 0, bufferSize, 0, &mappedData);

        fixed (byte* dataPtr = data)
        {
            System.Buffer.MemoryCopy(dataPtr, mappedData, (long)bufferSize, (long)bufferSize);
        }

        _context.VulkanApi.UnmapMemory(_context.Device, memory);

        _buffers[buffer.Handle] = (memory, bufferSize);
        _performanceMetrics?.RecordBufferCreated();
        return new VkBuffer(buffer.Handle);
    }

    /// <inheritdoc />
    public VkBuffer CreateIndexBuffer(ReadOnlySpan<byte> data)
    {
        var bufferSize = (ulong)data.Length;

        var bufferInfo = new BufferCreateInfo
        {
            SType = StructureType.BufferCreateInfo,
            Size = bufferSize,
            Usage = BufferUsageFlags.IndexBufferBit,
            SharingMode = SharingMode.Exclusive,
        };

        VkBuffer buffer;
        if (
            _context.VulkanApi.CreateBuffer(_context.Device, &bufferInfo, null, &buffer)
            != Result.Success
        )
        {
            throw new Exception("Failed to create index buffer");
        }

        _context.VulkanApi.GetBufferMemoryRequirements(
            _context.Device,
            buffer,
            out var memRequirements
        );

        var allocInfo = new MemoryAllocateInfo
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = memRequirements.Size,
            MemoryTypeIndex = FindMemoryType(
                memRequirements.MemoryTypeBits,
                MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit
            ),
        };

        DeviceMemory memory;
        if (
            _context.VulkanApi.AllocateMemory(_context.Device, &allocInfo, null, &memory)
            != Result.Success
        )
        {
            _context.VulkanApi.DestroyBuffer(_context.Device, buffer, null);
            throw new Exception("Failed to allocate index buffer memory");
        }

        _context.VulkanApi.BindBufferMemory(_context.Device, buffer, memory, 0);

        void* mappedData;
        _context.VulkanApi.MapMemory(_context.Device, memory, 0, bufferSize, 0, &mappedData);

        fixed (byte* dataPtr = data)
        {
            System.Buffer.MemoryCopy(dataPtr, mappedData, (long)bufferSize, (long)bufferSize);
        }

        _context.VulkanApi.UnmapMemory(_context.Device, memory);

        _buffers[buffer.Handle] = (memory, bufferSize);
        _performanceMetrics?.RecordBufferCreated();
        return new VkBuffer(buffer.Handle);
    }

    /// <inheritdoc />
    public VkBuffer CreateUniformBuffer(ulong size)
    {
        // Create buffer with uniform buffer usage
        var bufferInfo = new BufferCreateInfo
        {
            SType = StructureType.BufferCreateInfo,
            Size = size,
            Usage = BufferUsageFlags.UniformBufferBit | BufferUsageFlags.TransferDstBit,
            SharingMode = SharingMode.Exclusive,
        };

        VkBuffer buffer;
        if (
            _context.VulkanApi.CreateBuffer(_context.Device, &bufferInfo, null, &buffer)
            != Result.Success
        )
        {
            throw new Exception("Failed to create uniform buffer");
        }

        // Allocate HOST_VISIBLE and HOST_COHERENT memory for easy CPU updates
        _context.VulkanApi.GetBufferMemoryRequirements(
            _context.Device,
            buffer,
            out var memRequirements
        );

        var allocInfo = new MemoryAllocateInfo
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = memRequirements.Size,
            MemoryTypeIndex = FindMemoryType(
                memRequirements.MemoryTypeBits,
                MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit
            ),
        };

        DeviceMemory memory;
        if (
            _context.VulkanApi.AllocateMemory(_context.Device, &allocInfo, null, &memory)
            != Result.Success
        )
        {
            _context.VulkanApi.DestroyBuffer(_context.Device, buffer, null);
            throw new Exception("Failed to allocate uniform buffer memory");
        }

        // Bind buffer to memory
        _context.VulkanApi.BindBufferMemory(_context.Device, buffer, memory, 0);

        _buffers[buffer.Handle] = (memory, size);
        _performanceMetrics?.RecordBufferCreated();
        return new VkBuffer(buffer.Handle);
    }

    /// <inheritdoc />
    public VkBuffer CreateStorageBuffer(ulong size)
    {
        var bufferInfo = new BufferCreateInfo
        {
            SType = StructureType.BufferCreateInfo,
            Size = size,
            Usage = BufferUsageFlags.StorageBufferBit,
            SharingMode = SharingMode.Exclusive,
        };

        VkBuffer buffer;
        if (
            _context.VulkanApi.CreateBuffer(_context.Device, &bufferInfo, null, &buffer)
            != Result.Success
        )
        {
            throw new Exception("Failed to create storage buffer");
        }

        _context.VulkanApi.GetBufferMemoryRequirements(
            _context.Device,
            buffer,
            out var memRequirements
        );

        var allocInfo = new MemoryAllocateInfo
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = memRequirements.Size,
            MemoryTypeIndex = FindMemoryType(
                memRequirements.MemoryTypeBits,
                MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit
            ),
        };

        DeviceMemory memory;
        if (
            _context.VulkanApi.AllocateMemory(_context.Device, &allocInfo, null, &memory)
            != Result.Success
        )
        {
            _context.VulkanApi.DestroyBuffer(_context.Device, buffer, null);
            throw new Exception("Failed to allocate storage buffer memory");
        }

        _context.VulkanApi.BindBufferMemory(_context.Device, buffer, memory, 0);

        _buffers[buffer.Handle] = (memory, size);
        _performanceMetrics?.RecordBufferCreated();
        return new VkBuffer(buffer.Handle);
    }

    /// <inheritdoc />
    public void UpdateBuffer(VkBuffer buffer, ReadOnlySpan<byte> data)
    {
        if (!_buffers.TryGetValue(buffer.Handle, out var bufferInfo))
        {
            throw new ArgumentException("Unknown buffer handle", nameof(buffer));
        }

        var size = (ulong)data.Length;
        if (size > bufferInfo.Size)
        {
            throw new ArgumentException("Data exceeds the buffer capacity", nameof(data));
        }

        // Map memory
        void* mappedData;
        _context.VulkanApi.MapMemory(_context.Device, bufferInfo.Memory, 0, size, 0, &mappedData);

        // Copy data
        fixed (byte* dataPtr = data)
        {
            System.Buffer.MemoryCopy(dataPtr, mappedData, (long)size, (long)size);
        }

        // Unmap (HOST_COHERENT flag means no need for explicit flush)
        _context.VulkanApi.UnmapMemory(_context.Device, bufferInfo.Memory);
    }

    /// <inheritdoc />
    public void DestroyBuffer(VkBuffer buffer)
    {
        if (!_buffers.TryRemove(buffer.Handle, out var bufferInfo))
        {
            return;
        }

        // Wait for GPU to finish using the buffer
        _context.VulkanApi.DeviceWaitIdle(_context.Device);

        _context.VulkanApi.DestroyBuffer(_context.Device, buffer, null);
        _context.VulkanApi.FreeMemory(_context.Device, bufferInfo.Memory, null);
        _performanceMetrics?.RecordBufferDestroyed();
    }

    /// <summary>
    /// Finds a physical-device memory type that supports the requested properties.
    /// </summary>
    /// <param name="typeFilter">The bit mask of memory types supported by a Vulkan resource.</param>
    /// <param name="properties">The memory properties required by the resource.</param>
    /// <returns>The index of a compatible memory type.</returns>
    /// <exception cref="Exception">Thrown when no compatible memory type is available.</exception>
    private uint FindMemoryType(uint typeFilter, MemoryPropertyFlags properties)
    {
        _context.VulkanApi.GetPhysicalDeviceMemoryProperties(
            _context.PhysicalDevice,
            out var memProperties
        );

        for (uint i = 0; i < memProperties.MemoryTypeCount; i++)
        {
            if (
                (typeFilter & (1 << (int)i)) != 0
                && (memProperties.MemoryTypes[(int)i].PropertyFlags & properties) == properties
            )
            {
                return i;
            }
        }

        throw new Exception("Failed to find suitable memory type");
    }
}
