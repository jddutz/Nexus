namespace Nexus.Graphics.Resources;

public class GeometryResourceDescription : IResourceDescription
{
    public string Name { get; }
    public string SourceFilePath { get; }

    public GeometryResourceDescription(string name, ShaderStageEnum stage, string sourceFilePath)
    {
        Name = name;
        SourceFilePath = new(sourceFilePath);
    }
}
