using VkBuffer = Silk.NET.Vulkan.Buffer;

namespace Nexus.Graphics.Vulkan.Components;

public unsafe class GeometryFactory(Context context, ILogger<GeometryFactory> logger)
    : IGeometryFactory
{
    private readonly Context _context = context;
    private readonly ILogger<GeometryFactory> _logger = logger;

    private readonly Dictionary<ResourceId, VkBuffer> _vertexBuffers = [];
    private readonly Dictionary<ResourceId, DeviceMemory> _vertexMemory = [];
    private readonly Dictionary<ResourceId, uint> _vertexCounts = [];

    public ResourceId Create(UniformColorVertexGeometryDefinition definition)
    {
        var id = definition.Id;

        if (_vertexBuffers.ContainsKey(id))
        {
            _logger.LogDebug(
                "Geometry already exists; returning existing Vulkan geometry resource. "
                    + "GeometryId={GeometryId}, VertexCount={VertexCount}, BufferHandle={BufferHandle}",
                id,
                _vertexCounts[id],
                _vertexBuffers[id].Handle
            );
            return id;
        }

        var vertices = definition.Vertices;

        _logger.LogDebug(
            "Creating Vulkan vertex geometry. GeometryId={GeometryId}, VertexCount={VertexCount}, "
                + "VertexStride={VertexStride}, AllocationSize={AllocationSize}",
            id,
            vertices.Length,
            Unsafe.SizeOf<Vertex>(),
            (ulong)vertices.Length * (ulong)Unsafe.SizeOf<Vertex>()
        );

        if (vertices.Length == 0)
            throw new InvalidOperationException($"Geometry '{id}' contains no vertices.");

        ulong size = (ulong)vertices.Length * (ulong)Unsafe.SizeOf<Vertex>();

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

            var memoryTypeIndex = FindMemoryType(
                requirements.MemoryTypeBits,
                MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit
            );

            var allocationInfo = new MemoryAllocateInfo
            {
                SType = StructureType.MemoryAllocateInfo,
                AllocationSize = requirements.Size,
                MemoryTypeIndex = memoryTypeIndex,
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
                fixed (Vertex* source = vertices.AsSpan())
                {
                    System.Buffer.MemoryCopy(source, mapped, size, size);
                }
            }
            finally
            {
                _context.VulkanApi.UnmapMemory(_context.Device, vertexMemory);
            }

            _vertexBuffers.Add(id, vertexBuffer);
            _vertexMemory.Add(id, vertexMemory);
            _vertexCounts.Add(id, (uint)vertices.Length);

            _logger.LogInformation(
                "Created Vulkan vertex geometry. GeometryId={GeometryId}, VertexCount={VertexCount}, "
                    + "BufferHandle={BufferHandle}, MemoryHandle={MemoryHandle}",
                id,
                vertices.Length,
                vertexBuffer.Handle,
                vertexMemory.Handle
            );

            return id;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Failed to create Vulkan vertex geometry. GeometryId={GeometryId}, "
                    + "VertexCount={VertexCount}, BufferHandle={BufferHandle}, MemoryHandle={MemoryHandle}",
                id,
                vertices.Length,
                vertexBuffer.Handle,
                vertexMemory.Handle
            );

            if (vertexMemory.Handle != 0)
                _context.VulkanApi.FreeMemory(_context.Device, vertexMemory, null);

            if (vertexBuffer.Handle != 0)
                _context.VulkanApi.DestroyBuffer(_context.Device, vertexBuffer, null);

            throw;
        }
    }

    public ResourceId Read(ResourceId id)
    {
        if (!_vertexBuffers.ContainsKey(id))
            throw new KeyNotFoundException($"Geometry resource '{id}' does not exist.");

        _logger.LogDebug("Read Vulkan geometry resource. GeometryId={GeometryId}", id);

        return id;
    }

    public VkBuffer ReadBuffer(ResourceId id)
    {
        if (!_vertexBuffers.TryGetValue(id, out var buffer))
            throw new KeyNotFoundException($"Geometry resource '{id}' does not exist.");

        _logger.LogDebug(
            "Read Vulkan geometry buffer. GeometryId={GeometryId}, BufferHandle={BufferHandle}",
            id,
            buffer.Handle
        );

        return buffer;
    }

    public uint ReadVertexCount(ResourceId id)
    {
        if (!_vertexCounts.TryGetValue(id, out var count))
            throw new KeyNotFoundException($"Geometry resource '{id}' does not exist.");

        _logger.LogDebug(
            "Read Vulkan geometry vertex count. GeometryId={GeometryId}, VertexCount={VertexCount}",
            id,
            count
        );

        return count;
    }

    public ResourceId Update(ResourceId id, UniformColorVertexGeometryDefinition definition)
    {
        if (!_vertexBuffers.ContainsKey(id))
            throw new KeyNotFoundException($"Geometry resource '{id}' does not exist.");

        _logger.LogDebug(
            "Updating Vulkan vertex geometry. GeometryId={GeometryId}, "
                + "NewVertexCount={VertexCount}",
            id,
            definition.Vertices.Length
        );

        Delete(id);
        Create(definition);

        _logger.LogInformation(
            "Updated Vulkan vertex geometry. GeometryId={GeometryId}, VertexCount={VertexCount}",
            id,
            definition.Vertices.Length
        );

        return id;
    }

    public ResourceId Delete(ResourceId id)
    {
        var bufferRemoved = _vertexBuffers.Remove(id, out var buffer);
        var memoryRemoved = _vertexMemory.Remove(id, out var memory);

        if (bufferRemoved)
        {
            _context.VulkanApi.DestroyBuffer(_context.Device, buffer, null);
        }

        if (memoryRemoved)
        {
            _context.VulkanApi.FreeMemory(_context.Device, memory, null);
        }

        var vertexCountRemoved = _vertexCounts.Remove(id);

        _logger.LogInformation(
            "Deleted Vulkan vertex geometry. GeometryId={GeometryId}, BufferRemoved={BufferRemoved}, "
                + "MemoryRemoved={MemoryRemoved}, VertexCountRemoved={VertexCountRemoved}",
            id,
            bufferRemoved,
            memoryRemoved,
            vertexCountRemoved
        );

        return id;
    }

    private uint FindMemoryType(uint typeFilter, MemoryPropertyFlags properties)
    {
        _context.VulkanApi.GetPhysicalDeviceMemoryProperties(
            _context.PhysicalDevice,
            out var memoryProperties
        );

        for (uint i = 0; i < memoryProperties.MemoryTypeCount; i++)
        {
            if (
                (typeFilter & (1u << (int)i)) != 0
                && (memoryProperties.MemoryTypes[(int)i].PropertyFlags & properties) == properties
            )
            {
                _logger.LogDebug(
                    "Selected Vulkan memory type for geometry. MemoryTypeIndex={MemoryTypeIndex}, "
                        + "RequiredProperties={RequiredProperties}",
                    i,
                    properties
                );

                return i;
            }
        }

        throw new InvalidOperationException(
            $"Unable to find Vulkan memory type with properties '{properties}'."
        );
    }
}
