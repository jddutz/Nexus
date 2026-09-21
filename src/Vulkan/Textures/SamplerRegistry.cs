namespace Nexus.Graphics.Vulkan.Textures;

public unsafe class SamplerRegistry(Context context, ILogger<SamplerRegistry> logger)
    : ISamplerRegistry
{
    private readonly Context _context = context;
    private readonly ILogger<SamplerRegistry> _logger = logger;
    private readonly Dictionary<RenderableId, Sampler> _samplers = [];
    private readonly Dictionary<Sampler, int> _refs = [];

    private Sampler CreateSampler(ISamplingBehavior behavior)
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

            MinLod = 0,
            MaxLod = behavior.MinFilter.UsesMipmaps() ? Vk.LodClampNone : 0,

            MipLodBias = 0,

            AnisotropyEnable = false,
            CompareEnable = false,

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
            throw new InvalidOperationException($"Unable to create Vulkan sampler: {result}");

        return sampler;
    }

    public Sampler Get(RenderableId id)
    {
        if (!_samplers.TryGetValue(id, out var sampler))
            throw new KeyNotFoundException($"Sampler '{id}' is not registered.");

        return sampler;
    }

    public Sampler Acquire(ISamplingBehavior samplingBehavior)
    {
        ArgumentNullException.ThrowIfNull(samplingBehavior);

        var id = new IdentityHashBuilder(nameof(SamplerRegistry))
            .Add(samplingBehavior.Id)
            .Compute();

        if (_samplers.TryGetValue(id, out var sampler))
        {
            var referenceCount = ++_refs[sampler];

            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug(
                    "Reusing sampler. ResourceId={ResourceId}, SamplingBehaviorId={SamplingBehaviorId}, SamplerHandle={SamplerHandle}, ReferenceCount={ReferenceCount}",
                    id,
                    samplingBehavior.Id,
                    sampler.Handle,
                    referenceCount
                );

            return sampler;
        }

        sampler = CreateSampler(samplingBehavior);

        _samplers.Add(id, sampler);
        _refs.Add(sampler, 1);

        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug(
                "Created sampler. ResourceId={ResourceId}, SamplingBehaviorId={SamplingBehaviorId}, SamplerHandle={SamplerHandle}",
                id,
                samplingBehavior.Id,
                sampler.Handle
            );

        return sampler;
    }

    public void Release(RenderableId id)
    {
        if (!_samplers.TryGetValue(id, out var sampler))
            return;

        var referenceCount = --_refs[sampler];

        if (referenceCount > 0)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug(
                    "Released sampler reference. ResourceId={ResourceId}, SamplerHandle={SamplerHandle}, ReferenceCount={ReferenceCount}",
                    id,
                    sampler.Handle,
                    referenceCount
                );

            return;
        }

        _refs.Remove(sampler);
        _samplers.Remove(id);

        _context.VulkanApi.DestroySampler(_context.Device, sampler, null);

        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug(
                "Destroyed sampler. ResourceId={ResourceId}, SamplerHandle={SamplerHandle}",
                id,
                sampler.Handle
            );
    }

    public void Reset()
    {
        foreach (var (id, sampler) in _samplers)
        {
            _context.VulkanApi.DestroySampler(_context.Device, sampler, null);

            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug(
                    "Reset sampler. ResourceId={ResourceId}, SamplerHandle={SamplerHandle}",
                    id,
                    sampler.Handle
                );
        }

        _samplers.Clear();
        _refs.Clear();
    }

    public void Dispose()
    {
        Reset();

        GC.SuppressFinalize(this);
    }
}
