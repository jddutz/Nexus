namespace Nexus.Graphics.Vulkan;

public unsafe class ShaderFactory(Context context) : IShaderFactory
{
    private readonly Context _context = context;
    private readonly Dictionary<DrawableId, ShaderModule> _modules = [];

    public DrawableId Create(IShaderContract description)
    {
        var id = description.Id;

        if (_modules.ContainsKey(id))
            return id;

        var module = CreateModule(description);

        _modules.Add(id, module);

        return id;
    }

    public ShaderModule Read(DrawableId id)
    {
        if (!_modules.TryGetValue(id, out var module))
            throw new KeyNotFoundException($"Shader resource '{id}' does not exist.");

        return module;
    }

    public void Update(DrawableId id, IShaderContract description)
    {
        if (!_modules.TryGetValue(id, out var existing))
            throw new KeyNotFoundException($"Shader resource '{id}' does not exist.");

        // Create the replacement first so a failed shader load leaves the
        // currently registered module intact.
        var replacement = CreateModule(description);

        _modules[id] = replacement;
        _context.VulkanApi.DestroyShaderModule(_context.Device, existing, null);
    }

    public void Delete(DrawableId id)
    {
        if (!_modules.Remove(id, out var module))
            return;

        _context.VulkanApi.DestroyShaderModule(_context.Device, module, null);
    }

    private ShaderModule CreateModule(IShaderContract shader)
    {
        var shaderPath = Path.Combine(
            AppContext.BaseDirectory,
            "Shaders",
            shader.SourceFileName + ".spv"
        );
        var code = File.ReadAllBytes(shaderPath);

        if (code.Length == 0)
            throw new InvalidOperationException($"Shader '{shader.Name}' contains no SPIR-V data.");

        if (code.Length % sizeof(uint) != 0)
            throw new InvalidOperationException(
                $"Shader '{shader.Name}' contains invalid SPIR-V data."
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
                    $"Unable to create shader module '{shader.Name}': {result}"
                );

            return module;
        }
    }
}
