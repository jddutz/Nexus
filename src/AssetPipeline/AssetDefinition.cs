namespace Nexus.AssetPipeline;

public sealed class AssetDefinition
{
    public string AssetType { get; set; } = string.Empty;
    public string ContentId { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    /// <summary>Gets or sets whether to export an MSDF atlas PNG beside the copied font.</summary>
    public bool IncludeMsdf { get; set; }
    public string Path { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
    public string[] Files { get; set; } = [];
}
