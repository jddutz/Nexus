namespace Nexus.AssetPipeline.Fonts;

public sealed record FontDefinition(
    string ContentId,
    string Source,
    FontGlyphRepertoire Glyphs,
    FontGenerationSettings Generation
)
{
    public static FontDefinition FromAsset(AssetDefinition asset)
    {
        PipelineLog.Info(
            $"FontDefinition.FromAsset input: ContentId='{asset.ContentId}', Source='{asset.Source}', "
                + $"Repertoire='{asset.Glyphs?.Repertoire}', Characters='{asset.Glyphs?.Characters}', "
                + $"Generation={(asset.Generation is null ? "default" : "explicit")}."
        );

        var result = new FontDefinition(
            asset.ContentId,
            asset.Source,
            asset.Glyphs ?? new FontGlyphRepertoire(),
            asset.Generation ?? new FontGenerationSettings()
        );

        PipelineLog.Info(
            $"FontDefinition.FromAsset returned: ContentId='{result.ContentId}', Source='{result.Source}', "
                + $"Repertoire='{result.Glyphs.Repertoire}', EmSize={result.Generation.EmSize}, "
                + $"DistanceRange={result.Generation.DistanceRange}, Padding={result.Generation.Padding}."
        );
        return result;
    }
}

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
        PipelineLog.Info(
            $"FontGlyphRepertoire.GetCodepoints called: Repertoire='{Repertoire}', Characters='{Characters}'."
        );
        if (!string.Equals(Repertoire, "ascii", StringComparison.OrdinalIgnoreCase))
            throw new FontBuildException($"Unsupported glyph repertoire '{Repertoire}'.");

        var result = PrintableAscii
            .EnumerateRunes()
            .Concat(Characters.EnumerateRunes())
            .Select(rune => rune.Value)
            .Distinct()
            .Order()
            .ToArray();
        PipelineLog.Info(
            $"FontGlyphRepertoire.GetCodepoints returned Count={result.Length}, "
                + $"First={(result.Length == 0 ? "none" : result[0])}, Last={(result.Length == 0 ? "none" : result[^1])}."
        );
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
        PipelineLog.Info(
            $"FontGenerationSettings.Validate called: EmSize={EmSize}, DistanceRange={DistanceRange}, Padding={Padding}."
        );
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
        PipelineLog.Info("FontGenerationSettings.Validate returned successfully.");
    }
}
