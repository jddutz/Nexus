namespace Nexus.Assets.Fonts;

/// <summary>
/// Builds complete font atlas and runtime metadata from a font source.
/// </summary>
public interface IFontBuilder
{
    /// <summary>
    /// Builds the requested glyphs and packages the generated atlas and metadata.
    /// </summary>
    /// <param name="sourcePath">The source TrueType or OpenType font path.</param>
    /// <param name="codepoints">The Unicode codepoints required by the font build.</param>
    /// <param name="settings">The atlas and distance-field generation settings.</param>
    /// <returns>The completed font build result.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="FontBuildException">The requested font result cannot be generated.</exception>
    FontBuildResult Build(
        string sourcePath,
        IReadOnlyList<int> codepoints,
        FontGenerationSettings settings
    );
}

/// <summary>
/// Reports an error while generating a font build result.
/// </summary>
public sealed class FontBuildException : Exception
{
    /// <summary>
    /// Initializes a font build exception with a message.
    /// </summary>
    /// <param name="message">The error description.</param>
    public FontBuildException(string message)
        : base(message) { }

    /// <summary>
    /// Initializes a font build exception with a message and cause.
    /// </summary>
    /// <param name="message">The error description.</param>
    /// <param name="innerException">The exception that caused this error.</param>
    public FontBuildException(string message, Exception innerException)
        : base(message, innerException) { }
}
