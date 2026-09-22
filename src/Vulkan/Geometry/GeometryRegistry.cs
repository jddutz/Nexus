namespace Nexus.Graphics.Vulkan.Geometry;

public unsafe class VertexBufferRegistry(Context context, ILogger<VertexBufferRegistry> logger)
    : IGeometryRegistry
{
    private readonly Context _context = context;
    private readonly ILogger<VertexBufferRegistry> _logger = logger;
    private readonly Dictionary<(MeshId MeshId, VertexFormatId FormatId), VkBuffer> _buffers = [];
    private readonly Dictionary<VkBuffer, DeviceMemory> _memory = [];
    private readonly Dictionary<VkBuffer, int> _refs = [];

    public IEnumerable<IVulkanCommand> Create(IGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        yield break;
    }

    public IEnumerable<IVulkanCommand> Update(IGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        yield break;
    }

    public IEnumerable<IVulkanCommand> Release(IGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        Release(geometry.Id);
        yield break;
    }

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

    public VkBuffer Get(MeshId meshId, VertexFormatId formatId)
    {
        var key = (meshId, formatId);
        if (!_buffers.TryGetValue(key, out var buffer))
            throw new KeyNotFoundException(
                $"Vertex buffer for mesh '{meshId}' and vertex format '{formatId}' is not registered."
            );

        return buffer;
    }

    public VkBuffer Acquire(IDrawable renderable)
    {
        ArgumentNullException.ThrowIfNull(renderable);

        var shader =
            renderable.VertexShader
            ?? throw new InvalidOperationException(
                $"Renderable '{renderable.Id}' requires a vertex shader."
            );

        var mesh = renderable.Mesh;
        var format = shader.VertexFormat;
        var key = (mesh.Id, format.Id);

        if (_buffers.TryGetValue(key, out var buffer))
        {
            var referenceCount = ++_refs[buffer];
            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug(
                    "Reusing vertex buffer. ResourceId={ResourceId}, SourceId={SourceId}, VertexFormatId={VertexFormatId}, BufferHandle={BufferHandle}, ReferenceCount={ReferenceCount}",
                    mesh.Id,
                    mesh.Id,
                    format.Id,
                    buffer.Handle,
                    referenceCount
                );
            return buffer;
        }

        var data = new byte[checked((int)mesh.Count * (int)format.Stride)];
        mesh.WriteTo(0, checked((int)mesh.Count), format, data);

        buffer = CreateBuffer(data);

        _buffers.Add(key, buffer);
        _refs.Add(buffer, 1);

        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug(
                "Created vertex buffer. ResourceId={ResourceId}, SourceId={SourceId}, VertexFormatId={VertexFormatId}, BufferHandle={BufferHandle}, Size={Size}",
                mesh.Id,
                mesh.Id,
                format.Id,
                buffer.Handle,
                data.Length
            );

        return buffer;
    }

    public void Release(MeshId meshId)
    {
        foreach (var key in _buffers.Keys.Where(key => key.MeshId == meshId).ToArray())
        {
            var buffer = _buffers[key];
            var referenceCount = --_refs[buffer];
            if (referenceCount > 0)
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                    _logger.LogDebug(
                        "Released vertex buffer reference. MeshId={MeshId}, VertexFormatId={VertexFormatId}, BufferHandle={BufferHandle}, ReferenceCount={ReferenceCount}",
                        meshId,
                        key.FormatId,
                        buffer.Handle,
                        referenceCount
                    );
                continue;
            }

            _refs.Remove(buffer);
            _buffers.Remove(key);

            _context.VulkanApi.DestroyBuffer(_context.Device, buffer, null);
            if (_memory.Remove(buffer, out var memory))
                _context.VulkanApi.FreeMemory(_context.Device, memory, null);

            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug(
                    "Destroyed vertex buffer. MeshId={MeshId}, VertexFormatId={VertexFormatId}, BufferHandle={BufferHandle}",
                    meshId,
                    key.FormatId,
                    buffer.Handle
                );
        }
    }

    private void ResetBuffers()
    {
        foreach (var (key, buffer) in _buffers)
        {
            _context.VulkanApi.DestroyBuffer(_context.Device, buffer, null);
            if (_memory.Remove(buffer, out var memory))
                _context.VulkanApi.FreeMemory(_context.Device, memory, null);

            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug(
                    "Reset vertex buffer. ResourceId={ResourceId}, BufferHandle={BufferHandle}",
                    key.MeshId,
                    buffer.Handle
                );
        }

        _buffers.Clear();
        _memory.Clear();
        _refs.Clear();
    }

    public void Dispose()
    {
        ResetBuffers();

        GC.SuppressFinalize(this);
    }

    public IEnumerable<IVulkanCommand> Reset()
    {
        ResetBuffers();
        yield break;
    }
}
