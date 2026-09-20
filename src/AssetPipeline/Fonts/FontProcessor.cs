namespace Nexus.AssetPipeline.Fonts;

public sealed class FontProcessor
{
    private static readonly HashSet<string> SupportedExtensions = new(
        StringComparer.OrdinalIgnoreCase
    )
    {
        ".ttf",
        ".otf",
    };
    private readonly IFontRasterizer _rasterizer;

    public FontProcessor(IFontRasterizer rasterizer) => _rasterizer = rasterizer;

    public FontBuildResult Process(FontDefinition definition, string sourceRoot)
    {
        PipelineLog.Info(
            $"FontProcessor.Process called: ContentId='{definition?.ContentId}', Source='{definition?.Source}', SourceRoot='{sourceRoot}'."
        );
        ArgumentNullException.ThrowIfNull(definition);
        if (string.IsNullOrWhiteSpace(definition.ContentId))
            throw new FontBuildException("A Font asset must specify contentId.");
        if (
            definition.ContentId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || definition.ContentId.Contains('/')
            || definition.ContentId.Contains('\\')
        )
            throw new FontBuildException(
                $"Font '{definition.ContentId}' has an invalid contentId."
            );
        if (string.IsNullOrWhiteSpace(definition.Source))
            throw new FontBuildException(
                $"Font '{definition.ContentId}' must specify a source file."
            );

        var sourcePath = Path.GetFullPath(definition.Source, sourceRoot);
        PipelineLog.Info(
            $"FontProcessor.Process resolved source path '{sourcePath}'. Extension='{Path.GetExtension(sourcePath)}', Exists={File.Exists(sourcePath)}."
        );
        if (!SupportedExtensions.Contains(Path.GetExtension(sourcePath)))
            throw new FontBuildException(
                $"Font '{definition.ContentId}' source '{sourcePath}' must be a .ttf or .otf file."
            );
        if (!File.Exists(sourcePath))
            throw new FontBuildException(
                $"Font '{definition.ContentId}' source '{sourcePath}' does not exist."
            );

        try
        {
            definition.Generation.Validate();
            var codepoints = definition.Glyphs.GetCodepoints();
            PipelineLog.Info($"Calling rasterizer: CodepointCount={codepoints.Length}.");
            var result = _rasterizer.Rasterize(sourcePath, codepoints, definition.Generation);
            PipelineLog.Info(
                $"FontProcessor.Process returned atlas {result.Atlas.Width}x{result.Atlas.Height}, "
                    + $"Pixels={result.Atlas.Pixels.Length}, Glyphs={result.Glyphs.Count}, Kerning={result.Kerning.Count}."
            );
            return result;
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

    public static string GetOutputPath(string outputRoot, string contentId)
    {
        var result = Path.Combine(outputRoot, "fonts", contentId);
        PipelineLog.Info($"FontProcessor.GetOutputPath returned '{result}'.");
        return result;
    }
}
