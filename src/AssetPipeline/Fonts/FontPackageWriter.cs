using System.Text.Json;

namespace Nexus.AssetPipeline.Fonts;

public static class FontPackageWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static void Write(string packagePath, FontBuildResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        Validate(result);

        try
        {
            Directory.CreateDirectory(packagePath);
            File.WriteAllBytes(Path.Combine(packagePath, "atlas.rgb8"), result.Atlas.Pixels);
            var metadata = new
            {
                formatVersion = 1,
                atlas = new { result.Atlas.Width, result.Atlas.Height, format = FontAtlas.PixelFormat, file = "atlas.rgb8" },
                result.Metrics,
                result.Glyphs,
                result.Kerning,
                result.Msdf,
            };
            File.WriteAllText(
                Path.Combine(packagePath, "font.json"),
                JsonSerializer.Serialize(metadata, JsonOptions) + "\n"
            );
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new FontBuildException($"Could not serialize Font package '{packagePath}'.", exception);
        }
    }

    private static void Validate(FontBuildResult result)
    {
        if (result.Atlas.Width <= 0 || result.Atlas.Height <= 0)
            throw new FontBuildException("Rasterizer returned invalid atlas dimensions.");
        var expectedLength = checked(result.Atlas.Width * result.Atlas.Height * 3);
        if (result.Atlas.Pixels.Length != expectedLength)
            throw new FontBuildException(
                $"Rasterizer returned {result.Atlas.Pixels.Length} atlas bytes; expected {expectedLength}."
            );
        if (result.Glyphs.Count == 0)
            throw new FontBuildException("Rasterizer returned no glyphs.");
    }
}
