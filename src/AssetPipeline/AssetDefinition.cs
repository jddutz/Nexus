namespace Nexus.AssetPipeline;

public sealed class AssetDefinition
{
    public string AssetType { get; set; } = string.Empty;
    public string ContentId { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
    public string[] Files { get; set; } = [];
}
