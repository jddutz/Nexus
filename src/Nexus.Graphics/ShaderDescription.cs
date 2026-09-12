namespace Nexus.Graphics;

public sealed record ShaderDescription
{
    public required ShaderStageEnum Stage { get; init; }
    public required byte[] Code { get; init; }
    public string EntryPoint { get; init; } = "main";
}
