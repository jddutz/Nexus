namespace Nexus.Graphics.Vulkan;

/// <summary>Loads built-in SPIR-V resources and application-provided shader files.</summary>
internal static class ShaderLoader
{
    /// <summary>Loads an embedded built-in shader or an external custom shader.</summary>
    /// <param name="sourceFileName">The shader source file name, without the appended .spv extension.</param>
    /// <returns>The compiled SPIR-V bytes.</returns>
    /// <exception cref="FileNotFoundException">Neither an embedded resource nor a custom shader file exists.</exception>
    internal static byte[] Load(string sourceFileName)
    {
        var resourceName = $"Nexus.Graphics.Vulkan.Shaders.{sourceFileName}.spv";
        using var stream = typeof(ShaderLoader).Assembly.GetManifestResourceStream(resourceName);

        if (stream is not null)
        {
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            return memory.ToArray();
        }

        var shaderPath = Path.Combine(AppContext.BaseDirectory, "Shaders", sourceFileName + ".spv");
        return File.ReadAllBytes(shaderPath);
    }
}
