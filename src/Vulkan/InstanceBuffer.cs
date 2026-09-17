using VkBuffer = Silk.NET.Vulkan.Buffer;

namespace Nexus.Graphics.Vulkan;

/// <summary>
/// Provides a growable, persistently mapped vertex buffer for one frame's instance records.
/// </summary>
internal sealed unsafe class InstanceBuffer(Context context) : IDisposable
{
    private const ulong InitialCapacity = 4 * 1024;

    private readonly Context _context = context;
    private readonly List<(VkBuffer Buffer, DeviceMemory Memory)> _retiredResources = [];
    private DeviceMemory _memory;
    private void* _mappedData;
    private ulong _capacity;
    private ulong _writeOffset;
    private bool _disposed;

    /// <summary>
    /// Gets the Vulkan buffer that contains the uploaded instance records.
    /// </summary>
    public VkBuffer Buffer { get; private set; }

    /// <summary>
    /// Resets the allocation cursor after the frame fence has completed.
    /// </summary>
    public void Reset()
    {
        ReleaseRetiredResources();
        _writeOffset = 0;
    }

    /// <summary>
    /// Copies instance data into this frame's buffer and returns its byte offset.
    /// </summary>
    /// <param name="data">The contiguous instance records to upload.</param>
    /// <returns>The byte offset at which <paramref name="data"/> was written.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the buffer has been disposed.</exception>
    public ulong Write(ReadOnlySpan<byte> data)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var dataLength = checked((ulong)data.Length);
        var requiredCapacity = checked(_writeOffset + dataLength);
        EnsureCapacity(requiredCapacity);

        fixed (byte* source = data)
        {
            System.Buffer.MemoryCopy(
                source,
                (byte*)_mappedData + _writeOffset,
                checked((long)(_capacity - _writeOffset)),
                checked((long)dataLength)
            );
        }

        var offset = _writeOffset;
        _writeOffset = requiredCapacity;
        return offset;
    }

    /// <summary>
    /// Releases the Vulkan buffer, its mapped memory, and its device-memory allocation.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        ReleaseRetiredResources();
        DisposeResources();
        _disposed = true;
    }

    /// <summary>
    /// Ensures that the buffer can contain the requested number of bytes.
    /// </summary>
    /// <param name="requiredCapacity">The required capacity in bytes.</param>
    private void EnsureCapacity(ulong requiredCapacity)
    {
        if (requiredCapacity <= _capacity)
            return;

        var newCapacity = Math.Max(requiredCapacity, Math.Max(InitialCapacity, _capacity * 2));
        Recreate(newCapacity);
    }

    /// <summary>
    /// Recreates the buffer with the requested capacity and persistently maps its memory.
    /// </summary>
    /// <param name="capacity">The new capacity in bytes.</param>
    /// <exception cref="InvalidOperationException">Thrown when Vulkan resource creation fails.</exception>
    private void Recreate(ulong capacity)
    {
        RetireResources();

        var bufferInfo = new BufferCreateInfo
        {
            SType = StructureType.BufferCreateInfo,
            Size = capacity,
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
                out _memory
            );

            if (result != Result.Success)
                throw new InvalidOperationException(
                    $"Unable to allocate instance buffer memory: {result}"
                );

            result = _context.VulkanApi.BindBufferMemory(_context.Device, buffer, _memory, 0);
            if (result != Result.Success)
                throw new InvalidOperationException(
                    $"Unable to bind instance buffer memory: {result}"
                );

            void* mappedData = null;
            result = _context.VulkanApi.MapMemory(
                _context.Device,
                _memory,
                0,
                capacity,
                0,
                &mappedData
            );
            if (result != Result.Success)
                throw new InvalidOperationException(
                    $"Unable to map instance buffer memory: {result}"
                );

            _mappedData = mappedData;
            Buffer = buffer;
            _capacity = capacity;
        }
        catch
        {
            _context.VulkanApi.DestroyBuffer(_context.Device, buffer, null);
            DisposeResources();
            throw;
        }
    }

    /// <summary>
    /// Retires the current allocation until the frame fence permits releasing it.
    /// </summary>
    private void RetireResources()
    {
        if (_mappedData is not null)
        {
            _context.VulkanApi.UnmapMemory(_context.Device, _memory);
            _mappedData = null;
        }

        if (Buffer.Handle != 0 || _memory.Handle != 0)
        {
            _retiredResources.Add((Buffer, _memory));
            Buffer = default;
            _memory = default;
        }

        _capacity = 0;
    }

    /// <summary>
    /// Releases retired resources after the owning frame fence has completed.
    /// </summary>
    private void ReleaseRetiredResources()
    {
        foreach (var resource in _retiredResources)
        {
            if (resource.Buffer.Handle != 0)
                _context.VulkanApi.DestroyBuffer(_context.Device, resource.Buffer, null);

            if (resource.Memory.Handle != 0)
                _context.VulkanApi.FreeMemory(_context.Device, resource.Memory, null);
        }

        _retiredResources.Clear();
    }

    /// <summary>
    /// Releases the currently allocated Vulkan resources without changing disposal state.
    /// </summary>
    private void DisposeResources()
    {
        if (_mappedData is not null)
        {
            _context.VulkanApi.UnmapMemory(_context.Device, _memory);
            _mappedData = null;
        }

        if (Buffer.Handle != 0)
        {
            _context.VulkanApi.DestroyBuffer(_context.Device, Buffer, null);
            Buffer = default;
        }

        if (_memory.Handle != 0)
        {
            _context.VulkanApi.FreeMemory(_context.Device, _memory, null);
            _memory = default;
        }

        _capacity = 0;
    }

    /// <summary>
    /// Finds a physical-device memory type that supports the requested properties.
    /// </summary>
    /// <param name="typeFilter">The bit mask of memory types supported by the resource.</param>
    /// <param name="properties">The memory properties required for the allocation.</param>
    /// <returns>The index of a compatible memory type.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no compatible memory type exists.</exception>
    private uint FindMemoryType(uint typeFilter, MemoryPropertyFlags properties)
    {
        _context.VulkanApi.GetPhysicalDeviceMemoryProperties(
            _context.PhysicalDevice,
            out var memoryProperties
        );

        for (uint index = 0; index < memoryProperties.MemoryTypeCount; index++)
        {
            if (
                (typeFilter & (1 << (int)index)) != 0
                && (memoryProperties.MemoryTypes[(int)index].PropertyFlags & properties)
                    == properties
            )
            {
                return index;
            }
        }

        throw new InvalidOperationException("No compatible Vulkan memory type was found.");
    }
}
