namespace Nexus.Graphics.Vulkan.Textures;

public unsafe class SamplerRegistry(Context context) : ISamplerRegistry
{
    private readonly Context _context = context;

    private readonly Dictionary<ResourceId, Sampler> _samplers = [];

    private unsafe Sampler CreateSampler(ISamplingBehavior behavior)
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

        if (
            _context.VulkanApi.CreateSampler(_context.Device, &createInfo, null, out var sampler)
            != Result.Success
        )
            throw new InvalidOperationException("Failed to create Vulkan sampler.");

        return sampler;
    }

    public Sampler Get(ResourceId id)
    {
        if (!_samplers.TryGetValue(id, out var sampler))
            throw new KeyNotFoundException($"Sampler '{id}' is not registered.");

        return sampler;
    }

    public Sampler GetOrCreate(ISamplingBehavior samplingBehavior)
    {
        if (_samplers.TryGetValue(samplingBehavior.Id, out var sampler))
            return sampler;

        sampler = CreateSampler(samplingBehavior);

        _samplers.Add(samplingBehavior.Id, sampler);

        return sampler;
    }

    public void Release(ResourceId id)
    {
        if (!_samplers.Remove(id, out var sampler))
            return;

        _context.VulkanApi.DestroySampler(_context.Device, sampler, null);
    }

    public void Dispose()
    {
        foreach (var sampler in _samplers.Values)
        {
            _context.VulkanApi.DestroySampler(_context.Device, sampler, null);
        }

        _samplers.Clear();

        GC.SuppressFinalize(this);
    }
}
