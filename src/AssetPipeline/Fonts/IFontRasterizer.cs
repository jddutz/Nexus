namespace Nexus.AssetPipeline.Fonts;

public interface IFontRasterizer
{
    FontBuildResult Rasterize(
        string sourcePath,
        IReadOnlyList<int> codepoints,
        FontGenerationSettings settings
    );
}

public sealed class FontBuildException : Exception
{
    public FontBuildException(string message) : base(message) { }
    public FontBuildException(string message, Exception innerException) : base(message, innerException) { }
}
