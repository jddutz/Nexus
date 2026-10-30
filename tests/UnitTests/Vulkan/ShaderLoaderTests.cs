using System.Buffers.Binary;
using Nexus.Graphics.Vulkan;

namespace Tests;

/// <summary>Verifies shader loading without a Vulkan device or loose built-in shader files.</summary>
public sealed class ShaderLoaderTests
{
    /// <summary>Every shipped shader must be embedded with the name used by the runtime loader.</summary>
    /// <param name="sourceFileName">The built-in shader source name.</param>
    [Theory]
    [InlineData("biaxial_gradient.frag")]
    [InlineData("biaxial_gradient.vert")]
    [InlineData("image_texture.frag")]
    [InlineData("image_texture.vert")]
    [InlineData("linear_gradient.frag")]
    [InlineData("linear_gradient.vert")]
    [InlineData("masked_textured_quad.frag")]
    [InlineData("masked_textured_quad.vert")]
    [InlineData("msdf_text.frag")]
    [InlineData("msdf_text.vert")]
    [InlineData("per_vertex_color.frag")]
    [InlineData("per_vertex_color.vert")]
    [InlineData("radial_gradient.frag")]
    [InlineData("radial_gradient.vert")]
    [InlineData("shader.frag")]
    [InlineData("shader.vert")]
    [InlineData("textured_quad.frag")]
    [InlineData("textured_quad.vert")]
    [InlineData("ui.frag")]
    [InlineData("ui.vert")]
    [InlineData("uniform_color.frag")]
    [InlineData("uniform_color.vert")]
    public void Load_builtin_reads_embedded_spirv(string sourceFileName)
    {
        using var resource = typeof(VulkanGraphicsSystem).Assembly.GetManifestResourceStream(
            $"Nexus.Graphics.Vulkan.Shaders.{sourceFileName}.spv");
        Assert.NotNull(resource);
        using var expected = new MemoryStream();
        resource.CopyTo(expected);

        var code = ShaderLoader.Load(sourceFileName);

        Assert.Equal(expected.ToArray(), code);
        Assert.True(code.Length >= 20);
        Assert.Equal(0, code.Length % sizeof(uint));
        Assert.Equal(0x07230203u, BinaryPrimitives.ReadUInt32LittleEndian(code));
    }

    /// <summary>Custom shaders continue to load from the application's Shaders directory.</summary>
    [Fact]
    public void Load_custom_reads_external_file()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "Shaders");
        Directory.CreateDirectory(directory);
        var sourceFileName = $"custom-{Guid.NewGuid():N}.vert";
        var shaderPath = Path.Combine(directory, sourceFileName + ".spv");
        byte[] expected = [3, 2, 35, 7];

        try
        {
            File.WriteAllBytes(shaderPath, expected);

            Assert.Equal(expected, ShaderLoader.Load(sourceFileName));
        }
        finally
        {
            File.Delete(shaderPath);
        }
    }

    /// <summary>Missing shaders fail explicitly rather than returning empty bytecode.</summary>
    [Fact]
    public void Load_missing_throws_file_not_found()
    {
        var sourceFileName = $"missing-{Guid.NewGuid():N}.frag";

        var exception = Assert.Throws<FileNotFoundException>(() => ShaderLoader.Load(sourceFileName));

        Assert.Equal(
            Path.Combine(AppContext.BaseDirectory, "Shaders", sourceFileName + ".spv"),
            exception.FileName);
    }
}
