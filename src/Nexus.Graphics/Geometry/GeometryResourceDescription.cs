namespace Nexus.Graphics.Geometry;

public class GeometryResourceDescription : IResourceDescription
{
    public ResourceId Id { get; }
    public string Name { get; }
    public string SourceFilePath { get; }

    public GeometryResourceDescription(string name, ShaderStageEnum stage, string sourceFilePath)
    {
        Name = name;
        SourceFilePath = new(sourceFilePath);

        Id = new IdentityHashBuilder(nameof(GeometryResourceDescription))
            .Add(Name)
            .Add(SourceFilePath)
            .Compute();
    }
}
