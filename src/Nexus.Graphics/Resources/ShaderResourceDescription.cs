namespace Nexus.Graphics.Resources;

public class ShaderResourceDescription : IResourceDescription
{
    public string Name { get; }
    public ShaderStageEnum Stage { get; }
    public string Source { get; }

    public ShaderResourceDescription(
        string name,
        ShaderStageEnum stage = ShaderStageEnum.Undefined,
        string? source = null
    )
    {
        Name = name;
        Stage = stage;
        Source = source ?? string.Empty;
    }
}
