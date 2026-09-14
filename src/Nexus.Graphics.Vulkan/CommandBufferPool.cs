using VkCommandPool = Silk.NET.Vulkan.CommandPool;

namespace Nexus.Graphics.Vulkan;

/// <summary>
/// Owns a fixed set of primary Vulkan command buffers and manages their non-blocking reuse.
/// Each buffer is associated with the fence for its most recent submission.
/// </summary>
/// <remarks>
/// <para><strong>Responsibilities:</strong></para>
/// <list type="bullet">
/// <item>Creates the native command pool and allocates its fixed command-buffer set.</item>
/// <item>Returns a buffer only when its previous submission has completed.</item>
/// <item>Resets reusable command buffers before returning them.</item>
/// <item>Releases all owned command buffers and the native pool during disposal.</item>
/// </list>
/// <para>
/// The pool does not own, reset, wait on, or dispose submission fences. It only queries
/// fence status to determine whether a command buffer can be reused.
/// </para>
/// <para>
/// Command pools are not thread-safe in Vulkan. Use one instance per rendering thread,
/// or provide external synchronization when sharing an instance.
/// </para>
/// </remarks>
public unsafe class CommandBufferPool : ICommandBufferPool
{
    private struct CommandBufferAllocation
    {
        public CommandBuffer CommandBuffer;
        public Fence Fence;
    }

    private Context _context = null!;
    private VkCommandPool _vkCommandPool;

    // tracking
    public long FailedAcquisitionCount { get; private set; }
    public long TrimCount { get; private set; }
    public long ResetCount { get; private set; }

    private CommandBufferAllocation[] _commandBuffers = [];
    private bool _disposed;

    /// <summary>
    /// Prevents construction without the initialization performed by ForGraphics.
    /// </summary>
    private CommandBufferPool() { }

    /// <summary>
    /// Creates a graphics command pool with a fixed set of primary command buffers.
    /// </summary>
    /// <param name="context">Graphics context providing Vulkan device access.</param>
    /// <param name="maxBufferCount">Exact number of primary command buffers owned by the pool.</param>
    /// <returns>A command pool configured for graphics command-buffer reuse.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no graphics queue family is available or Vulkan cannot create the pool.
    /// </exception>
    public static CommandBufferPool ForGraphics(Context context, uint maxBufferCount)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (maxBufferCount == 0 || maxBufferCount > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxBufferCount),
                "The command pool capacity must be between 1 and Int32.MaxValue."
            );
        }

        var queueFamilyIndex =
            context.FindQueueFamily(QueueFlags.GraphicsBit)
            ?? throw new InvalidOperationException(
                "Failed to find a queue family that supports graphics commands."
            );

        var flags = CommandPoolCreateFlags.ResetCommandBufferBit;

        var poolInfo = new CommandPoolCreateInfo
        {
            SType = StructureType.CommandPoolCreateInfo,
            QueueFamilyIndex = queueFamilyIndex,
            Flags = flags,
        };

        var result = context.VulkanApi.CreateCommandPool(
            context.Device,
            &poolInfo,
            null,
            out VkCommandPool pool
        );
        if (result != Result.Success)
        {
            throw new InvalidOperationException($"Failed to create command pool: {result}");
        }

        var buffers = new CommandBuffer[maxBufferCount];

        var allocInfo = new CommandBufferAllocateInfo
        {
            SType = StructureType.CommandBufferAllocateInfo,
            CommandPool = pool,
            Level = CommandBufferLevel.Primary,
            CommandBufferCount = maxBufferCount,
        };

        fixed (CommandBuffer* pBuffers = buffers)
        {
            var allocationResult = context.VulkanApi.AllocateCommandBuffers(
                context.Device,
                &allocInfo,
                pBuffers
            );

            if (allocationResult != Result.Success)
            {
                context.VulkanApi.DestroyCommandPool(context.Device, pool, null);

                throw new InvalidOperationException(
                    $"Failed to allocate command buffers: {allocationResult}"
                );
            }
        }

        var commandBuffers = new CommandBufferAllocation[buffers.Length];

        for (var i = 0; i < buffers.Length; i++)
        {
            commandBuffers[i].CommandBuffer = buffers[i];
        }

        return new CommandBufferPool
        {
            _context = context,
            _vkCommandPool = pool,
            _commandBuffers = commandBuffers,
        };
    }

    /// <inheritdoc/>
    public bool TryGetCommandBuffer(Fence fence, out CommandBuffer commandBuffer)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_commandBuffers)
        {
            foreach (ref var allocation in _commandBuffers.AsSpan())
            {
                if (allocation.Fence.Handle != 0)
                {
                    var fenceStatus = _context.VulkanApi.GetFenceStatus(
                        _context.Device,
                        allocation.Fence
                    );

                    if (fenceStatus == Result.NotReady)
                        continue;

                    if (fenceStatus != Result.Success)
                        throw new InvalidOperationException(
                            $"Failed to query command buffer fence status: {fenceStatus}"
                        );

                    var resetResult = _context.VulkanApi.ResetCommandBuffer(
                        allocation.CommandBuffer,
                        CommandBufferResetFlags.None
                    );

                    if (resetResult != Result.Success)
                        throw new InvalidOperationException(
                            $"Failed to reset command buffer: {resetResult}"
                        );
                }

                allocation.Fence = fence;
                commandBuffer = allocation.CommandBuffer;
                return true;
            }
        }

        commandBuffer = default;
        return false;
    }

    /// <inheritdoc/>
    public void Reset(CommandPoolResetFlags flags = CommandPoolResetFlags.None)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var result = _context.VulkanApi.ResetCommandPool(_context.Device, _vkCommandPool, flags);
        if (result != Result.Success)
        {
            throw new InvalidOperationException($"Failed to reset command pool: {result}");
        }

        ResetCount++;
    }

    /// <inheritdoc/>
    public void Trim()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // vkTrimCommandPool is a Vulkan 1.1 feature
        // For now, just track the call - actual implementation requires checking feature support
        TrimCount++;
    }

    /// <summary>
    /// Releases all command buffers owned by this pool and destroys the native command pool.
    /// The pool does not dispose the fences associated with command-buffer submissions.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        // Free all tracked command buffers
        if (_commandBuffers.Length > 0)
        {
            lock (_commandBuffers)
            {
                var buffers = _commandBuffers
                    .Select(allocation => allocation.CommandBuffer)
                    .Where(buffer => buffer.Handle != 0)
                    .ToArray();
                if (buffers.Length > 0)
                {
                    fixed (CommandBuffer* pBuffers = buffers)
                    {
                        _context.VulkanApi.FreeCommandBuffers(
                            _context.Device,
                            _vkCommandPool,
                            (uint)buffers.Length,
                            pBuffers
                        );
                    }
                }
                Array.Clear(_commandBuffers);
            }
        }

        // Destroy the command pool
        if (_vkCommandPool.Handle != 0)
        {
            _context.VulkanApi.DestroyCommandPool(_context.Device, _vkCommandPool, null);
            _vkCommandPool = default;
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
