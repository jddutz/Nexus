namespace Nexus.Graphics.Vulkan.Descriptors;

/// <summary>
/// Allocates, writes, and releases Vulkan descriptor sets from a single fixed-capacity
/// native descriptor pool.
/// </summary>
/// <remarks>
/// <para><strong>Responsibilities:</strong></para>
/// <list type="bullet">
/// <item>Creates the native descriptor pool with a fixed capacity.</item>
/// <item>Allocates descriptor sets compatible with a supplied layout.</item>
/// <item>Writes uniform-buffer and combined-image-sampler descriptors into existing allocations.</item>
/// <item>Releases individual descriptor sets back to the native pool.</item>
/// <item>Destroys the native descriptor pool during disposal.</item>
/// </list>
/// <para>
/// This pool owns only the native descriptor pool and the descriptor sets allocated from it.
/// It does not own the descriptor-set layouts, buffers, images, image views, or samplers
/// referenced by writes.
/// </para>
/// </remarks>
/// <remarks>
/// Creates a fixed-capacity descriptor-set pool.
/// </remarks>
/// <param name="context">Graphics context providing Vulkan device access.</param>
/// <param name="syncManager">Synchronization manager used to defer descriptor-set release.</param>
/// <param name="maxSets">Maximum number of descriptor sets the native pool can allocate.</param>
/// <param name="uniformBufferCount">
/// Number of uniform-buffer descriptors the native pool can allocate.
/// </param>
/// <param name="combinedImageSamplerCount">
/// Number of combined-image-sampler descriptors the native pool can allocate.
/// </param>
/// <exception cref="InvalidOperationException">
/// Thrown when Vulkan cannot create the descriptor pool.
/// </exception>
public unsafe class DescriptorSetPool : IDescriptorSetPool
{
    /// <summary>Default maximum number of descriptor sets the native pool can allocate.</summary>
    public const uint DefaultMaxSets = 512;

    /// <summary>Default number of uniform-buffer descriptors the native pool can allocate.</summary>
    public const uint DefaultUniformBufferCount = 256;

    /// <summary>
    /// Default number of combined-image-sampler descriptors the native pool can allocate.
    /// </summary>
    public const uint DefaultCombinedImageSamplerCount = 256;

    private readonly Context _context;
    private readonly ISyncManager _syncManager;
    private readonly Queue<DescriptorSet>[] _released;
    private VkDescriptorPool _vkDescriptorPool;
    private bool _disposed;

    /// <summary>Creates a descriptor pool with deferred frame-safe release.</summary>
    /// <param name="context">Graphics context providing Vulkan device access.</param>
    /// <param name="syncManager">Synchronization manager used to defer releases.</param>
    /// <param name="maxSets">Maximum number of descriptor sets.</param>
    /// <param name="uniformBufferCount">Number of uniform-buffer descriptors.</param>
    /// <param name="combinedImageSamplerCount">Number of image-sampler descriptors.</param>
    public DescriptorSetPool(
        Context context,
        ISyncManager syncManager,
        uint maxSets = DefaultMaxSets,
        uint uniformBufferCount = DefaultUniformBufferCount,
        uint combinedImageSamplerCount = DefaultCombinedImageSamplerCount
    )
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(syncManager);
        _context = context;
        _syncManager = syncManager;
        _released = CreateReleaseQueues(syncManager);
        _vkDescriptorPool = CreateNativePool(
            context,
            maxSets,
            uniformBufferCount,
            combinedImageSamplerCount
        );
        _syncManager.FrameCompleted += OnFrameCompleted;
    }

    /// <summary>
    /// Creates one descriptor-set retirement queue for each frame slot.
    /// </summary>
    /// <param name="syncManager">The synchronization manager that defines the frame slots.</param>
    /// <returns>The initialized retirement queues.</returns>
    private static Queue<DescriptorSet>[] CreateReleaseQueues(ISyncManager syncManager)
    {
        ArgumentNullException.ThrowIfNull(syncManager);
        var queues = new Queue<DescriptorSet>[checked((int)syncManager.MaxFramesInFlight)];
        for (var index = 0; index < queues.Length; index++)
            queues[index] = new Queue<DescriptorSet>();
        return queues;
    }

    /// <summary>
    /// Creates the native Vulkan descriptor pool with the requested fixed capacities.
    /// </summary>
    /// <param name="context">Graphics context providing Vulkan device access.</param>
    /// <param name="maxSets">Maximum number of descriptor sets the native pool can allocate.</param>
    /// <param name="uniformBufferCount">
    /// Number of uniform-buffer descriptors the native pool can allocate.
    /// </param>
    /// <param name="combinedImageSamplerCount">
    /// Number of combined-image-sampler descriptors the native pool can allocate.
    /// </param>
    /// <returns>The created native descriptor pool.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when Vulkan cannot create the descriptor pool.
    /// </exception>
    private static VkDescriptorPool CreateNativePool(
        Context context,
        uint maxSets,
        uint uniformBufferCount,
        uint combinedImageSamplerCount
    )
    {
        ArgumentNullException.ThrowIfNull(context);

        var poolSizes = stackalloc DescriptorPoolSize[2]
        {
            new DescriptorPoolSize
            {
                Type = DescriptorType.UniformBuffer,
                DescriptorCount = uniformBufferCount,
            },
            new DescriptorPoolSize
            {
                Type = DescriptorType.CombinedImageSampler,
                DescriptorCount = combinedImageSamplerCount,
            },
        };

        var poolInfo = new DescriptorPoolCreateInfo
        {
            SType = StructureType.DescriptorPoolCreateInfo,
            Flags = DescriptorPoolCreateFlags.FreeDescriptorSetBit,
            MaxSets = maxSets,
            PoolSizeCount = 2,
            PPoolSizes = poolSizes,
        };

        var result = context.VulkanApi.CreateDescriptorPool(
            context.Device,
            &poolInfo,
            null,
            out VkDescriptorPool pool
        );

        if (result != Result.Success)
        {
            throw new InvalidOperationException($"Failed to create descriptor pool: {result}");
        }

        return pool;
    }

    /// <inheritdoc />
    public DescriptorSet Allocate(DescriptorSetLayout layout)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var allocInfo = new DescriptorSetAllocateInfo
        {
            SType = StructureType.DescriptorSetAllocateInfo,
            DescriptorPool = _vkDescriptorPool,
            DescriptorSetCount = 1,
            PSetLayouts = &layout,
        };

        DescriptorSet descriptorSet;
        var result = _context.VulkanApi.AllocateDescriptorSets(
            _context.Device,
            &allocInfo,
            &descriptorSet
        );

        if (result != Result.Success)
        {
            throw new InvalidOperationException($"Failed to allocate descriptor set: {result}");
        }

        return descriptorSet;
    }

    /// <inheritdoc />
    public void WriteUniformBuffer(
        DescriptorSet descriptorSet,
        uint binding,
        VkBuffer buffer,
        ulong offset,
        ulong range
    )
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var bufferInfo = new DescriptorBufferInfo
        {
            Buffer = buffer,
            Offset = offset,
            Range = range,
        };

        var write = new WriteDescriptorSet
        {
            SType = StructureType.WriteDescriptorSet,
            DstSet = descriptorSet,
            DstBinding = binding,
            DstArrayElement = 0,
            DescriptorType = DescriptorType.UniformBuffer,
            DescriptorCount = 1,
            PBufferInfo = &bufferInfo,
        };

        _context.VulkanApi.UpdateDescriptorSets(_context.Device, 1, &write, 0, null);
    }

    /// <inheritdoc />
    public void WriteCombinedImageSampler(
        DescriptorSet descriptorSet,
        uint binding,
        ImageView imageView,
        Sampler sampler
    )
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var imageInfo = new DescriptorImageInfo
        {
            Sampler = sampler,
            ImageView = imageView,
            ImageLayout = ImageLayout.ShaderReadOnlyOptimal,
        };

        var write = new WriteDescriptorSet
        {
            SType = StructureType.WriteDescriptorSet,
            DstSet = descriptorSet,
            DstBinding = binding,
            DstArrayElement = 0,
            DescriptorType = DescriptorType.CombinedImageSampler,
            DescriptorCount = 1,
            PImageInfo = &imageInfo,
        };

        _context.VulkanApi.UpdateDescriptorSets(_context.Device, 1, &write, 0, null);
    }

    /// <inheritdoc />
    public void Release(DescriptorSet descriptorSet)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var releaseFrameIndex = checked(
            (int)(
                (_syncManager.CurrentFrameIndex + _syncManager.MaxFramesInFlight - 1)
                % _syncManager.MaxFramesInFlight
            )
        );
        _released[releaseFrameIndex].Enqueue(descriptorSet);
    }

    /// <summary>
    /// Frees descriptor sets whose submitted frame has completed.
    /// </summary>
    /// <param name="sender">The synchronization manager.</param>
    /// <param name="e">The completed frame event data.</param>
    private void OnFrameCompleted(object? sender, FrameCompletedEventArgs e)
    {
        var releaseFrameIndex = checked((int)e.FrameIndex);
        if (releaseFrameIndex >= _released.Length)
            throw new ArgumentOutOfRangeException(nameof(e), e.FrameIndex, "Invalid frame index.");

        var queue = _released[releaseFrameIndex];
        while (queue.TryDequeue(out var descriptorSet))
        {
            var result = _context.VulkanApi.FreeDescriptorSets(
                _context.Device,
                _vkDescriptorPool,
                1,
                &descriptorSet
            );

            if (result != Result.Success)
                throw new InvalidOperationException($"Failed to release descriptor set: {result}");
        }
    }

    /// <summary>
    /// Destroys the native descriptor pool. All descriptor sets allocated from it are
    /// implicitly freed and must not be used afterward.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _syncManager.FrameCompleted -= OnFrameCompleted;

        if (_vkDescriptorPool.Handle != 0)
        {
            _context.VulkanApi.DestroyDescriptorPool(_context.Device, _vkDescriptorPool, null);
            _vkDescriptorPool = default;
        }

        GC.SuppressFinalize(this);
    }
}
