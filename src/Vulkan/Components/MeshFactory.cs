namespace Nexus.Graphics.Vulkan.Components;

/// <summary>
/// Creates Vulkan vertex buffers from general packed mesh data.
/// </summary>
public unsafe class MeshFactory(Context context, ILogger<MeshFactory> logger) : IMeshFactory
{
    private readonly Context _context = context;
    private readonly ILogger<MeshFactory> _logger = logger;
    private readonly Dictionary<ResourceId, VkBuffer> _vertexBuffers = [];
    private readonly Dictionary<ResourceId, DeviceMemory> _vertexMemory = [];
    private readonly Dictionary<ResourceId, uint> _vertexCounts = [];

    /// <inheritdoc/>
    public ResourceId Create(MeshDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (definition.VertexCount == 0)
            throw new InvalidOperationException($"Mesh '{definition.Id}' contains no vertices.");
        if (definition.VertexCount > uint.MaxValue)
            throw new InvalidOperationException(
                $"Mesh '{definition.Id}' exceeds Vulkan's vertex-count limit."
            );
        if (_vertexBuffers.ContainsKey(definition.Id))
            return definition.Id;

        var size = checked((ulong)definition.VertexData.Length);
        VkBuffer vertexBuffer = default;
        DeviceMemory vertexMemory = default;

        try
        {
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
                out vertexBuffer
            );
            if (result != Result.Success)
                throw new InvalidOperationException($"Unable to create vertex buffer: {result}");

            _context.VulkanApi.GetBufferMemoryRequirements(
                _context.Device,
                vertexBuffer,
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
                out vertexMemory
            );
            if (result != Result.Success)
                throw new InvalidOperationException($"Unable to allocate vertex memory: {result}");

            result = _context.VulkanApi.BindBufferMemory(
                _context.Device,
                vertexBuffer,
                vertexMemory,
                0
            );
            if (result != Result.Success)
                throw new InvalidOperationException($"Unable to bind vertex memory: {result}");

            void* mapped = null;
            result = _context.VulkanApi.MapMemory(
                _context.Device,
                vertexMemory,
                0,
                size,
                0,
                &mapped
            );
            if (result != Result.Success)
                throw new InvalidOperationException($"Unable to map vertex memory: {result}");

            try
            {
                fixed (byte* source = definition.VertexData.Span)
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
                _context.VulkanApi.UnmapMemory(_context.Device, vertexMemory);
            }

            _vertexBuffers.Add(definition.Id, vertexBuffer);
            _vertexMemory.Add(definition.Id, vertexMemory);
            _vertexCounts.Add(definition.Id, (uint)definition.VertexCount);
            _logger.LogInformation(
                "Created Vulkan mesh. MeshId={MeshId}, VertexCount={VertexCount}, VertexStride={VertexStride}",
                definition.Id,
                definition.VertexCount,
                definition.VertexStride
            );
            return definition.Id;
        }
        catch
        {
            if (vertexMemory.Handle != 0)
                _context.VulkanApi.FreeMemory(_context.Device, vertexMemory, null);
            if (vertexBuffer.Handle != 0)
                _context.VulkanApi.DestroyBuffer(_context.Device, vertexBuffer, null);
            throw;
        }
    }

    /// <inheritdoc/>
    public VkBuffer ReadBuffer(ResourceId id) =>
        _vertexBuffers.TryGetValue(id, out var buffer)
            ? buffer
            : throw new KeyNotFoundException($"Mesh resource '{id}' does not exist.");

    /// <inheritdoc/>
    public uint ReadVertexCount(ResourceId id) =>
        _vertexCounts.TryGetValue(id, out var count)
            ? count
            : throw new KeyNotFoundException($"Mesh resource '{id}' does not exist.");

    /// <inheritdoc/>
    public ResourceId Delete(ResourceId id)
    {
        if (_vertexBuffers.Remove(id, out var buffer))
            _context.VulkanApi.DestroyBuffer(_context.Device, buffer, null);
        if (_vertexMemory.Remove(id, out var memory))
            _context.VulkanApi.FreeMemory(_context.Device, memory, null);
        _vertexCounts.Remove(id);
        return id;
    }

    /// <summary>
    /// Finds a physical-device memory type that supports the requested properties.
    /// </summary>
    /// <param name="typeFilter">The bit mask of supported memory types.</param>
    /// <param name="properties">The required memory properties.</param>
    /// <returns>The index of a compatible memory type.</returns>
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
}
