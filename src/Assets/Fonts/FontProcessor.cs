namespace Nexus.Assets.Fonts;

/// <summary>
/// Adapts NAP font asset definitions to the Typography builder and adds asset context to failures.
/// </summary>
public sealed class FontProcessor
{
    private static readonly HashSet<string> SupportedExtensions = new(
        StringComparer.OrdinalIgnoreCase
    )
    {
        ".ttf",
        ".otf",
    };
    private readonly IFontBuilder _builder;

    /// <summary>
    /// Initializes a processor with the font builder used to generate font data.
    /// </summary>
    /// <param name="builder">The Typography builder.</param>
    public FontProcessor(IFontBuilder builder) => _builder = builder;

    /// <summary>
    /// Validates and builds a font asset from its normalized definition.
    /// </summary>
    /// <param name="definition">The normalized font asset definition.</param>
    /// <param name="sourceRoot">The root used to resolve the source font path.</param>
    /// <returns>The generated font atlas and runtime metadata.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="definition"/> is null.</exception>
    /// <exception cref="FontBuildException">The definition is invalid or the font cannot be built.</exception>
    public FontBuildResult Process(FontDefinition definition, string sourceRoot)
    {
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
            var result = _builder.Build(sourcePath, codepoints, definition.Generation);
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

    /// <summary>
    /// Gets the package directory for a font content identifier.
    /// </summary>
    /// <param name="outputRoot">The content output root.</param>
    /// <param name="contentId">The font content identifier.</param>
    /// <returns>The font package directory.</returns>
    public static string GetOutputPath(string outputRoot, string contentId)
    {
        var result = Path.Combine(outputRoot, "fonts", contentId);
        return result;
    }
}
