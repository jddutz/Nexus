namespace Nexus.Graphics.Vulkan.Textures;

/// <summary>
/// Manages shared Vulkan samplers keyed by sampling behavior identities.
/// </summary>
public unsafe class SamplerRegistry : ISamplerRegistry
{
    private readonly Context _context;
    private readonly ISyncManager _syncManager;

    private readonly Dictionary<SamplingBehaviorId, VkSampler> _samplers = [];
    private readonly Dictionary<SamplingBehaviorId, int> _refs = [];
    private readonly Queue<VkSampler>[] _released;

    /// <summary>
    /// Creates a sampler registry with frame-slot deferred-release queues.
    /// </summary>
    /// <param name="context">The Vulkan context that owns the samplers.</param>
    /// <param name="syncManager">The synchronization manager used to defer sampler destruction.</param>
    public SamplerRegistry(Context context, ISyncManager syncManager)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _syncManager = syncManager ?? throw new ArgumentNullException(nameof(syncManager));
        _released = new Queue<VkSampler>[checked((int)syncManager.MaxFramesInFlight)];

        for (var index = 0; index < _released.Length; index++)
            _released[index] = new Queue<VkSampler>();

        _syncManager.FrameCompleted += OnFrameCompleted;
    }

    /// <inheritdoc/>
    public void Create(ISamplingBehavior behavior)
    {
        ArgumentNullException.ThrowIfNull(behavior);

        if (_samplers.ContainsKey(behavior.Id))
        {
            _refs[behavior.Id]++;
            return;
        }

        var sampler = CreateSampler(behavior);

        _samplers.Add(behavior.Id, sampler);
        _refs.Add(behavior.Id, 1);
    }

    /// <inheritdoc/>
    public VkSampler Get(SamplingBehaviorId id)
    {
        if (!_samplers.TryGetValue(id, out var sampler))
            throw new KeyNotFoundException($"Sampler for sampling behavior '{id}' is not registered.");

        return sampler;
    }

    /// <inheritdoc/>
    public void Release(ISamplingBehavior behavior)
    {
        ArgumentNullException.ThrowIfNull(behavior);

        if (!_samplers.TryGetValue(behavior.Id, out var sampler))
            return;

        var referenceCount = --_refs[behavior.Id];

        if (referenceCount > 0)
            return;

        _refs.Remove(behavior.Id);
        _samplers.Remove(behavior.Id);
        QueueRelease(sampler);
    }

    /// <inheritdoc/>
    public void Reset()
    {
        foreach (var sampler in _samplers.Values)
            DestroySampler(sampler);

        _samplers.Clear();
        _refs.Clear();

        foreach (var queue in _released)
        {
            while (queue.TryDequeue(out var sampler))
                DestroySampler(sampler);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _syncManager.FrameCompleted -= OnFrameCompleted;
        Reset();
        GC.SuppressFinalize(this);
    }

    private VkSampler CreateSampler(ISamplingBehavior behavior)
    {
        var createInfo = new SamplerCreateInfo
        {
            SType = StructureType.SamplerCreateInfo,
            MagFilter = behavior.MagFilter.ToVulkanFilter(),
            MinFilter = behavior.MinFilter.ToVulkanFilter(),
            MipmapMode = behavior.MinFilter.ToVulkanMipmapMode(),
            AddressModeU = behavior.WrapU.ToVulkanAddressMode(),
            AddressModeV = behavior.WrapV.ToVulkanAddressMode(),
            AddressModeW = SamplerAddressMode.ClampToEdge,
            MipLodBias = 0,
            AnisotropyEnable = false,
            MaxAnisotropy = 1,
            CompareEnable = false,
            CompareOp = CompareOp.Always,
            MinLod = 0,
            MaxLod = behavior.MinFilter.UsesMipmaps() ? float.MaxValue : 0,
            BorderColor = BorderColor.IntOpaqueBlack,
            UnnormalizedCoordinates = false,
        };

        var result = _context.VulkanApi.CreateSampler(
            _context.Device,
            in createInfo,
            null,
            out var sampler
        );

        if (result != Result.Success)
            throw new InvalidOperationException($"Failed to create Vulkan sampler: {result}.");

        return sampler;
    }

    private void QueueRelease(VkSampler sampler)
    {
        var releaseFrameIndex = checked(
            (int)(
                (_syncManager.CurrentFrameIndex + _syncManager.MaxFramesInFlight - 1)
                % _syncManager.MaxFramesInFlight
            )
        );

        _released[releaseFrameIndex].Enqueue(sampler);
    }

    private void DestroySampler(VkSampler sampler) =>
        _context.VulkanApi.DestroySampler(_context.Device, sampler, null);

    private void OnFrameCompleted(object? sender, FrameCompletedEventArgs e)
    {
        var releaseFrameIndex = checked((int)e.FrameIndex);

        if (releaseFrameIndex >= _released.Length)
            throw new ArgumentOutOfRangeException(nameof(e), e.FrameIndex, "Invalid frame index.");

        var samplers = _released[releaseFrameIndex];

        while (samplers.TryDequeue(out var sampler))
            DestroySampler(sampler);
    }
}