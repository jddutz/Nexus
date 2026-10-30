using Nexus.Core.Performance;
namespace Nexus.Graphics.Vulkan;

/// <summary>Creates and manages Vulkan shader modules.</summary>
public unsafe class ShaderFactory(Context context, IPerformanceTelemetry? telemetry = null) : IShaderFactory
{
    private readonly Context _context = context;
    private readonly Dictionary<DrawableId, ShaderModule> _modules = [];

    /// <inheritdoc />
    public DrawableId Create(IShaderContract description)
    {
        using var timing = new LoadPerformanceScope(telemetry, "shader.realize", description.SourceFileName);
        var id = description.Id;

        if (_modules.ContainsKey(id))
        {
            telemetry?.RecordCache("shader.registry", description.SourceFileName, true);
            return id;
        }
        telemetry?.RecordCache("shader.registry", description.SourceFileName, false);

        var module = CreateModule(description);

        _modules.Add(id, module);

        return id;
    }

    /// <inheritdoc />
    public ShaderModule Read(DrawableId id)
    {
        if (!_modules.TryGetValue(id, out var module))
            throw new KeyNotFoundException($"Shader resource '{id}' does not exist.");

        return module;
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
    public void Delete(DrawableId id)
    {
        if (!_modules.Remove(id, out var module))
            return;

        _context.VulkanApi.DestroyShaderModule(_context.Device, module, null);
    }

    /// <summary>Loads SPIR-V data and creates a native shader module.</summary>
    /// <param name="shader">The shader contract to realize.</param>
    /// <returns>The created Vulkan shader module.</returns>
    private ShaderModule CreateModule(IShaderContract shader)
    {
        byte[] code;
        using (var timing = new LoadPerformanceScope(telemetry, "shader.file.read", shader.SourceFileName))
            code = ShaderLoader.Load(shader.SourceFileName);
        using var creation = new LoadPerformanceScope(telemetry, "shader.module.create", shader.SourceFileName, units: code.Length);

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
