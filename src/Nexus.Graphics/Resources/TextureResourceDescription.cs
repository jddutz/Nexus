namespace Nexus.Graphics.Resources;

public class TextureResourceDescription : IResourceDescription
{
    public string Name { get; }
    public string SourceFilePath { get; }

    public TextureResourceDescription(string name, string sourceFilePath)
    {
        Name = name;
        SourceFilePath = sourceFilePath;
    }
}
