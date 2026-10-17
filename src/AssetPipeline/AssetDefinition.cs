namespace Nexus.AssetPipeline;

public sealed class AssetDefinition
{
    public string AssetType { get; set; } = string.Empty;
    public string ContentId { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;

    /// <summary>Gets or sets whether to export an MSDF atlas PNG beside the copied font.</summary>
    public bool IncludeMsdf { get; set; }

    /// <summary>Gets or sets whether to include decoded rasterizer input in the font manifest entry.</summary>
    public bool IncludeRasterizerInput { get; set; }

    public TextureRegionSettings? Regions { get; set; }

    public string? TextureFormat { get; set; }
    public bool? Mipmaps { get; set; }
    /// <summary>Optional companion metadata format: aseprite or none (default).</summary>
    public string? Metadata { get; set; }
    public int? JpegQuality { get; set; }
    public int? TargetImageWidth { get; set; }
    public int? TargetImageHeight { get; set; }
    /// <summary>shrink (default), fit, fill, or stretch; requires both target dimensions.</summary>
    public string ImageSizeMode { get; set; } = "shrink";

    public string Path { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
    public string[] Files { get; set; } = [];
}
