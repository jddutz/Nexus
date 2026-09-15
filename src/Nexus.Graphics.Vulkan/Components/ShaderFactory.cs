namespace Nexus.Graphics.Vulkan.Components;

public unsafe class ShaderFactory(Context context) : IShaderFactory
{
    private readonly Context _context = context;
    private readonly Dictionary<ResourceId, ShaderModule> _modules = [];

    public ResourceId Create(ShaderDefinition definition)
    {
        var id = definition.Id;

        if (_modules.ContainsKey(id))
            return id;

        var module = CreateModule(definition);

        _modules.Add(id, module);

        return id;
    }

    public ShaderModule Read(ResourceId id)
    {
        if (!_modules.TryGetValue(id, out var module))
            throw new KeyNotFoundException($"Shader resource '{id}' does not exist.");

        return module;
    }

    public void Update(ResourceId id, ShaderDefinition definition)
    {
        if (!_modules.TryGetValue(id, out var existing))
            throw new KeyNotFoundException($"Shader resource '{id}' does not exist.");

        // Create the replacement first so a failed shader load leaves the
        // currently registered module intact.
        var replacement = CreateModule(definition);

        _modules[id] = replacement;
        _context.VulkanApi.DestroyShaderModule(_context.Device, existing, null);
    }

    public void Delete(ResourceId id)
    {
        if (!_modules.Remove(id, out var module))
            return;

        _context.VulkanApi.DestroyShaderModule(_context.Device, module, null);
    }

    private ShaderModule CreateModule(ShaderDefinition definition)
    {
        var shaderPath = Path.Combine(AppContext.BaseDirectory, "Shaders", definition.Source);
        var code = File.ReadAllBytes(shaderPath);

        if (code.Length == 0)
            throw new InvalidOperationException(
                $"Shader '{definition.Name}' contains no SPIR-V data."
            );

        if (code.Length % sizeof(uint) != 0)
            throw new InvalidOperationException(
                $"Shader '{definition.Name}' contains invalid SPIR-V data."
            );

        fixed (byte* codePtr = code)
        {
            var createInfo = new ShaderModuleCreateInfo
            {
                SType = StructureType.ShaderModuleCreateInfo,
                CodeSize = (nuint)code.Length,
                PCode = (uint*)codePtr,
            };

            var result = _context.VulkanApi.CreateShaderModule(
                _context.Device,
                in createInfo,
                null,
                out var module
            );

            if (result != Result.Success)
                throw new InvalidOperationException(
                    $"Unable to create shader module '{definition.Name}': {result}"
                );

            return module;
        }
    }
}
