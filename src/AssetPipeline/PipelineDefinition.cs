namespace Nexus.AssetPipeline;

public sealed class PipelineDefinition
{
    public string Root { get; set; } = ".";
    public string TextureFormat { get; set; } = "ktx2";
    public bool Mipmaps { get; set; } = true;
    public int JpegQuality { get; set; } = 90;
    public AssetDefinition[] Assets { get; set; } = [];
}
