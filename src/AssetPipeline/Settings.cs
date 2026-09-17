namespace Nexus.AssetPipeline;

public sealed class AssetPipelineSettings
{
    public string OutputFolder { get; set; } = "./Content";
    public string[] InputFiles { get; set; } = [];
}
