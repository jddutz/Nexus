namespace Nexus.AssetPipeline;

public sealed class PipelineDefinition
{
    public string Root { get; set; } = ".";
    public AssetDefinition[] Assets { get; set; } = [];
}
