namespace Nexus.AssetPipeline.Fonts;

public sealed class FontProcessor
{
    private static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".ttf", ".otf" };
    private readonly IFontRasterizer _rasterizer;

    public FontProcessor(IFontRasterizer rasterizer) => _rasterizer = rasterizer;

    public FontBuildResult Process(FontDefinition definition, string sourceRoot)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (string.IsNullOrWhiteSpace(definition.ContentId))
            throw new FontBuildException("A Font asset must specify contentId.");
        if (definition.ContentId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            definition.ContentId.Contains('/') || definition.ContentId.Contains('\\'))
            throw new FontBuildException($"Font '{definition.ContentId}' has an invalid contentId.");
        if (string.IsNullOrWhiteSpace(definition.Source))
            throw new FontBuildException($"Font '{definition.ContentId}' must specify a source file.");

        var sourcePath = Path.GetFullPath(definition.Source, sourceRoot);
        if (!SupportedExtensions.Contains(Path.GetExtension(sourcePath)))
            throw new FontBuildException(
                $"Font '{definition.ContentId}' source '{sourcePath}' must be a .ttf or .otf file."
            );
        if (!File.Exists(sourcePath))
            throw new FontBuildException($"Font '{definition.ContentId}' source '{sourcePath}' does not exist.");

        try
        {
            definition.Generation.Validate();
            var codepoints = definition.Glyphs.GetCodepoints();
            return _rasterizer.Rasterize(sourcePath, codepoints, definition.Generation);
        }
        catch (FontBuildException exception)
        {
            throw new FontBuildException(
                $"Failed to build Font '{definition.ContentId}' from '{sourcePath}': {exception.Message}",
                exception
            );
        }
        catch (Exception exception)
        {
            throw new FontBuildException(
                $"Failed to build Font '{definition.ContentId}' from '{sourcePath}'.",
                exception
            );
        }
    }

    public static string GetOutputPath(string outputRoot, string contentId) =>
        Path.Combine(outputRoot, "fonts", contentId);
}
