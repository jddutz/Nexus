namespace Nexus.Assets.Fonts;

public sealed record FontDefinition(
    string ContentId,
    string Source,
    FontGlyphRepertoire Glyphs,
    FontGenerationSettings Generation
) { }

public sealed class FontGlyphRepertoire
{
    public const string PrintableAscii =
        " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~";

    /// <summary>Named repertoire. The initial vertical slice supports <c>ascii</c>.</summary>
    public string Repertoire { get; set; } = "ascii";

    /// <summary>Additional Unicode scalar values, in source order.</summary>
    public string Characters { get; set; } = string.Empty;

    public int[] GetCodepoints()
    {
        if (!string.Equals(Repertoire, "ascii", StringComparison.OrdinalIgnoreCase))
            throw new FontBuildException($"Unsupported glyph repertoire '{Repertoire}'.");

        var result = PrintableAscii
            .EnumerateRunes()
            .Concat(Characters.EnumerateRunes())
            .Select(rune => rune.Value)
            .Distinct()
            .Order()
            .ToArray();
        return result;
    }
}

public sealed class FontGenerationSettings
{
    public int EmSize { get; set; } = 48;
    public double DistanceRange { get; set; } = 4;
    public int Padding { get; set; } = 2;

    internal void Validate()
    {
        if (EmSize is < 16 or > 256)
            throw new FontBuildException(
                "Font generation emSize must be between 16 and 256 pixels."
            );
        if (DistanceRange is < 1 or > 32)
            throw new FontBuildException(
                "Font generation distanceRange must be between 1 and 32 pixels."
            );
        if (Padding is < 0 or > 32)
            throw new FontBuildException(
                "Font generation padding must be between 0 and 32 pixels."
            );
    }
}
