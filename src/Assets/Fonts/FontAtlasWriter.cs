namespace Nexus.Assets.Fonts;

/// <summary>
/// Writes the binary atlas artifact produced by the font builder.
/// </summary>
public static class FontAtlasWriter
{
    /// <summary>
    /// Writes the generated atlas to the specified artifact path.
    /// </summary>
    /// <param name="atlasPath">The path of the atlas artifact to write.</param>
    /// <param name="result">The generated font data containing the atlas.</param>
    public static void Write(string atlasPath, FontBuildResult result)
    {
        ArgumentNullException.ThrowIfNull(atlasPath);
        ArgumentNullException.ThrowIfNull(result);
        Validate(result);

        try
        {
            var directory = Path.GetDirectoryName(atlasPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllBytes(atlasPath, result.Atlas.Pixels);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new FontBuildException($"Could not write font atlas '{atlasPath}'.", exception);
        }
    }

    /// <summary>
    /// Validates the atlas dimensions, pixel count, and glyph payload.
    /// </summary>
    /// <param name="result">The generated font data to validate.</param>
    private static void Validate(FontBuildResult result)
    {
        if (result.Atlas.Width <= 0 || result.Atlas.Height <= 0)
            throw new FontBuildException("Font builder returned invalid atlas dimensions.");

        var expectedLength = checked(result.Atlas.Width * result.Atlas.Height * 3);
        if (result.Atlas.Pixels.Length != expectedLength)
            throw new FontBuildException(
                $"Font builder returned {result.Atlas.Pixels.Length} atlas bytes; expected {expectedLength}."
            );
        if (result.Glyphs.Count == 0)
            throw new FontBuildException("Font builder returned no glyphs.");
    }
}
